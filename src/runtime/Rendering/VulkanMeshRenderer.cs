using AssemblyEngine.Core;
using Silk.NET.Vulkan;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace AssemblyEngine.Rendering;

[SupportedOSPlatform("windows")]
internal sealed unsafe class VulkanMeshRenderer : IDisposable
{
    private readonly Vk _vk;
    private Instance _instance;
    private PhysicalDevice _physicalDevice;
    private Device _device;
    private Queue _queue;
    private uint _queueFamilyIndex;
    private CommandPool _commandPool;
    private CommandBuffer _commandBuffer;
    private Fence _fence;

    private RenderPass _renderPass;
    private PipelineLayout _pipelineLayout;
    private Pipeline _fillPipeline;
    private Pipeline _wirePipeline;
    private bool _hasFillModeNonSolid;

    private Image _colorImage;
    private DeviceMemory _colorMemory;
    private ImageView _colorView;
    private Image _depthImage;
    private DeviceMemory _depthMemory;
    private ImageView _depthView;
    private Framebuffer _framebuffer;
    private int _rtWidth, _rtHeight;

    private VkBuffer _vertexBuffer;
    private DeviceMemory _vertexMemory;
    private nint _vertexMapped;
    private ulong _vertexCapacity;
    private VkBuffer _indexBuffer;
    private DeviceMemory _indexMemory;
    private nint _indexMapped;
    private ulong _indexCapacity;
    private VkBuffer _readbackBuffer;
    private DeviceMemory _readbackMemory;
    private nint _readbackMapped;
    private ulong _readbackCapacity;

    private readonly List<DrawCall> _draws = [];
    private readonly List<float> _vertices = [];
    private readonly List<uint> _indices = [];

    public bool HasPendingDraws => _draws.Count > 0;

    private VulkanMeshRenderer(Vk vk) => _vk = vk;

    public static VulkanMeshRenderer? TryCreate()
    {
        try
        {
            var vk = Vk.GetApi();
            var renderer = new VulkanMeshRenderer(vk);
            if (renderer.Initialize())
                return renderer;

            renderer.Dispose();
            return null;
        }
        catch
        {
            return null;
        }
    }

    public void QueueDraw(Mesh mesh, Matrix4x4 mvp, Color color, bool wireframe)
    {
        var baseVertex = _vertices.Count / 3;
        var firstIndex = _indices.Count;

        foreach (var v in mesh.Vertices)
        {
            _vertices.Add(v.Position.X);
            _vertices.Add(v.Position.Y);
            _vertices.Add(v.Position.Z);
        }

        foreach (var idx in mesh.Indices)
            _indices.Add((uint)(idx + baseVertex));

        _draws.Add(new DrawCall(firstIndex, mesh.Indices.Count,
            mvp,
            color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f,
            wireframe));
    }

    public bool Flush(Color clearColor, RenderSurface target)
    {
        if (_draws.Count == 0 || target.Width <= 0 || target.Height <= 0)
            return false;

        try
        {
            return FlushCore(clearColor, target);
        }
        catch
        {
            ClearBatch();
            return false;
        }
    }

    private bool FlushCore(Color clearColor, RenderSurface target)
    {
        if (_draws.Count == 0 || target.Width <= 0 || target.Height <= 0)
            return false;

        EnsureRenderTarget(target.Width, target.Height);
        if (_framebuffer.Handle == 0)
            return false;

        var vertexBytes = (ulong)(_vertices.Count * sizeof(float));
        var indexBytes = (ulong)(_indices.Count * sizeof(uint));
        if (vertexBytes == 0 || indexBytes == 0)
        {
            ClearBatch();
            return false;
        }

        EnsureHostBuffer(ref _vertexBuffer, ref _vertexMemory, ref _vertexMapped, ref _vertexCapacity,
            vertexBytes, BufferUsageFlags.VertexBufferBit);
        EnsureHostBuffer(ref _indexBuffer, ref _indexMemory, ref _indexMapped, ref _indexCapacity,
            indexBytes, BufferUsageFlags.IndexBufferBit);

        if (_vertexMapped == 0 || _indexMapped == 0)
        {
            ClearBatch();
            return false;
        }

        var vertexArray = CollectionsMarshal.AsSpan(_vertices);
        var indexArray = CollectionsMarshal.AsSpan(_indices);
        fixed (float* src = vertexArray)
            System.Buffer.MemoryCopy(src, (void*)_vertexMapped, (long)_vertexCapacity, (long)vertexBytes);

        fixed (uint* src = indexArray)
            System.Buffer.MemoryCopy(src, (void*)_indexMapped, (long)_indexCapacity, (long)indexBytes);

        RecordAndSubmit(clearColor, target.Width, target.Height);
        ReadBackToSurface(target);
        ClearBatch();
        return true;
    }

    public void Dispose()
    {
        if (_device.Handle != 0)
            _vk.DeviceWaitIdle(_device);

        DestroyRenderTargets();
        DestroyHostBuffer(ref _vertexBuffer, ref _vertexMemory, ref _vertexMapped, ref _vertexCapacity);
        DestroyHostBuffer(ref _indexBuffer, ref _indexMemory, ref _indexMapped, ref _indexCapacity);
        DestroyHostBuffer(ref _readbackBuffer, ref _readbackMemory, ref _readbackMapped, ref _readbackCapacity);

        if (_fillPipeline.Handle != 0) _vk.DestroyPipeline(_device, _fillPipeline, null);
        if (_wirePipeline.Handle != 0) _vk.DestroyPipeline(_device, _wirePipeline, null);
        if (_pipelineLayout.Handle != 0) _vk.DestroyPipelineLayout(_device, _pipelineLayout, null);
        if (_renderPass.Handle != 0) _vk.DestroyRenderPass(_device, _renderPass, null);
        if (_fence.Handle != 0) _vk.DestroyFence(_device, _fence, null);
        if (_commandPool.Handle != 0) _vk.DestroyCommandPool(_device, _commandPool, null);
        if (_device.Handle != 0) _vk.DestroyDevice(_device, null);
        if (_instance.Handle != 0) _vk.DestroyInstance(_instance, null);
        _vk.Dispose();
        GC.SuppressFinalize(this);
    }

    private bool Initialize()
    {
        if (!CreateInstance()) return false;
        if (!PickPhysicalDevice()) return false;
        if (!CreateDevice()) return false;
        if (!CreateCommandResources()) return false;
        if (!CreateRenderPass()) return false;
        return CreatePipelines();
    }

    private bool CreateInstance()
    {
        var appName = Marshal.StringToHGlobalAnsi("AssemblyEngine.MeshRenderer");
        try
        {
            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)appName,
                ApplicationVersion = 1,
                ApiVersion = Vk.Version11
            };
            var ci = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo
            };
            return _vk.CreateInstance(in ci, null, out _instance) == Result.Success;
        }
        finally { Marshal.FreeHGlobal(appName); }
    }

    private bool PickPhysicalDevice()
    {
        uint count = 0;
        _vk.EnumeratePhysicalDevices(_instance, ref count, null);
        if (count == 0) return false;

        var devices = new PhysicalDevice[count];
        fixed (PhysicalDevice* ptr = devices)
            _vk.EnumeratePhysicalDevices(_instance, ref count, ptr);

        foreach (var dev in devices)
        {
            uint qfCount = 0;
            _vk.GetPhysicalDeviceQueueFamilyProperties(dev, ref qfCount, null);
            var families = new QueueFamilyProperties[qfCount];
            fixed (QueueFamilyProperties* ptr = families)
                _vk.GetPhysicalDeviceQueueFamilyProperties(dev, ref qfCount, ptr);

            for (uint qi = 0; qi < families.Length; qi++)
            {
                if ((families[qi].QueueFlags & QueueFlags.GraphicsBit) == 0) continue;
                _physicalDevice = dev;
                _queueFamilyIndex = qi;

                _vk.GetPhysicalDeviceFeatures(dev, out var features);
                _hasFillModeNonSolid = features.FillModeNonSolid;
                return true;
            }
        }
        return false;
    }

    private bool CreateDevice()
    {
        var priority = 1f;
        var queueCi = new DeviceQueueCreateInfo
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = _queueFamilyIndex,
            QueueCount = 1,
            PQueuePriorities = &priority
        };

        PhysicalDeviceFeatures features = default;
        if (_hasFillModeNonSolid)
            features.FillModeNonSolid = true;

        var ci = new DeviceCreateInfo
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = &queueCi,
            PEnabledFeatures = &features
        };

        if (_vk.CreateDevice(_physicalDevice, in ci, null, out _device) != Result.Success)
            return false;

        _vk.GetDeviceQueue(_device, _queueFamilyIndex, 0, out _queue);
        return true;
    }

    private bool CreateCommandResources()
    {
        var poolCi = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _queueFamilyIndex,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit
        };
        if (_vk.CreateCommandPool(_device, in poolCi, null, out _commandPool) != Result.Success)
            return false;

        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };
        fixed (CommandBuffer* ptr = &_commandBuffer)
            if (_vk.AllocateCommandBuffers(_device, in allocInfo, ptr) != Result.Success)
                return false;

        var fenceCi = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        return _vk.CreateFence(_device, in fenceCi, null, out _fence) == Result.Success;
    }

    private bool CreateRenderPass()
    {
        var attachments = stackalloc AttachmentDescription[2];
        attachments[0] = new AttachmentDescription
        {
            Format = Format.B8G8R8A8Unorm,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.TransferSrcOptimal
        };
        attachments[1] = new AttachmentDescription
        {
            Format = Format.D32Sfloat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
        };

        var colorRef = new AttachmentReference { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };
        var depthRef = new AttachmentReference { Attachment = 1, Layout = ImageLayout.DepthStencilAttachmentOptimal };
        var subpass = new SubpassDescription
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorRef,
            PDepthStencilAttachment = &depthRef
        };

        var dependencies = stackalloc SubpassDependency[2];
        dependencies[0] = new SubpassDependency
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.BottomOfPipeBit,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            SrcAccessMask = 0,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit
        };
        dependencies[1] = new SubpassDependency
        {
            SrcSubpass = 0,
            DstSubpass = Vk.SubpassExternal,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstStageMask = PipelineStageFlags.TransferBit,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DstAccessMask = AccessFlags.TransferReadBit
        };

        var ci = new RenderPassCreateInfo
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 2,
            PAttachments = attachments,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 2,
            PDependencies = dependencies
        };
        return _vk.CreateRenderPass(_device, in ci, null, out _renderPass) == Result.Success;
    }

    private bool CreatePipelines()
    {
        var pushRange = new PushConstantRange
        {
            StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            Offset = 0,
            Size = 80
        };
        var layoutCi = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &pushRange
        };
        if (_vk.CreatePipelineLayout(_device, in layoutCi, null, out _pipelineLayout) != Result.Success)
            return false;

        _fillPipeline = CreateGraphicsPipeline(PolygonMode.Fill);
        if (_fillPipeline.Handle == 0) return false;

        if (_hasFillModeNonSolid)
            _wirePipeline = CreateGraphicsPipeline(PolygonMode.Line);

        return true;
    }

    private Pipeline CreateGraphicsPipeline(PolygonMode polygonMode)
    {
        var vertModule = CreateShaderModule(VulkanShaders.MeshVertexSpirV);
        var fragModule = CreateShaderModule(VulkanShaders.MeshFragmentSpirV);
        if (vertModule.Handle == 0 || fragModule.Handle == 0)
        {
            if (vertModule.Handle != 0) _vk.DestroyShaderModule(_device, vertModule, null);
            if (fragModule.Handle != 0) _vk.DestroyShaderModule(_device, fragModule, null);
            return default;
        }

        var entryName = Marshal.StringToHGlobalAnsi("main");
        try
        {
            var stages = stackalloc PipelineShaderStageCreateInfo[2];
            stages[0] = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = vertModule,
                PName = (byte*)entryName
            };
            stages[1] = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = fragModule,
                PName = (byte*)entryName
            };

            var bindingDesc = new VertexInputBindingDescription { Binding = 0, Stride = 12, InputRate = VertexInputRate.Vertex };
            var attrDesc = new VertexInputAttributeDescription { Location = 0, Binding = 0, Format = Format.R32G32B32Sfloat, Offset = 0 };
            var vertexInput = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &bindingDesc,
                VertexAttributeDescriptionCount = 1,
                PVertexAttributeDescriptions = &attrDesc
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList
            };

            var viewport = new Viewport();
            var scissor = new Rect2D();
            var viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                PViewports = &viewport,
                ScissorCount = 1,
                PScissors = &scissor
            };

            var raster = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                PolygonMode = polygonMode,
                CullMode = CullModeFlags.None,
                FrontFace = FrontFace.CounterClockwise,
                LineWidth = 1f
            };

            var msaa = new PipelineMultisampleStateCreateInfo
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = SampleCountFlags.Count1Bit
            };

            var depthStencil = new PipelineDepthStencilStateCreateInfo
            {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = true,
                DepthWriteEnable = true,
                DepthCompareOp = CompareOp.Less
            };

            var blendAttachment = new PipelineColorBlendAttachmentState
            {
                BlendEnable = true,
                SrcColorBlendFactor = BlendFactor.SrcAlpha,
                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
                AlphaBlendOp = BlendOp.Add,
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
            };
            var colorBlend = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                AttachmentCount = 1,
                PAttachments = &blendAttachment
            };

            var dynamicStates = stackalloc DynamicState[2] { DynamicState.Viewport, DynamicState.Scissor };
            var dynamicState = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 2,
                PDynamicStates = dynamicStates
            };

            var ci = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stages,
                PVertexInputState = &vertexInput,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &raster,
                PMultisampleState = &msaa,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlend,
                PDynamicState = &dynamicState,
                Layout = _pipelineLayout,
                RenderPass = _renderPass,
                Subpass = 0
            };

            Pipeline pipeline;
            _vk.CreateGraphicsPipelines(_device, default, 1, in ci, null, &pipeline);
            return pipeline;
        }
        finally
        {
            Marshal.FreeHGlobal(entryName);
            _vk.DestroyShaderModule(_device, vertModule, null);
            _vk.DestroyShaderModule(_device, fragModule, null);
        }
    }

    private ShaderModule CreateShaderModule(ReadOnlySpan<byte> spirv)
    {
        fixed (byte* ptr = spirv)
        {
            var ci = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)spirv.Length,
                PCode = (uint*)ptr
            };
            return _vk.CreateShaderModule(_device, in ci, null, out var module) == Result.Success ? module : default;
        }
    }

    private void EnsureRenderTarget(int width, int height)
    {
        if (_rtWidth == width && _rtHeight == height && _framebuffer.Handle != 0)
            return;

        if (_fence.Handle != 0)
        {
            var fence = _fence;
            _vk.WaitForFences(_device, 1, &fence, true, ulong.MaxValue);
        }
        DestroyRenderTargets();

        _colorImage = CreateImage(width, height, Format.B8G8R8A8Unorm,
            ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit, out _colorMemory);
        _colorView = CreateImageView(_colorImage, Format.B8G8R8A8Unorm, ImageAspectFlags.ColorBit);
        _depthImage = CreateImage(width, height, Format.D32Sfloat,
            ImageUsageFlags.DepthStencilAttachmentBit, out _depthMemory);
        _depthView = CreateImageView(_depthImage, Format.D32Sfloat, ImageAspectFlags.DepthBit);

        if (_colorView.Handle == 0 || _depthView.Handle == 0) return;

        var views = stackalloc ImageView[2] { _colorView, _depthView };
        var fbCi = new FramebufferCreateInfo
        {
            SType = StructureType.FramebufferCreateInfo,
            RenderPass = _renderPass,
            AttachmentCount = 2,
            PAttachments = views,
            Width = (uint)width,
            Height = (uint)height,
            Layers = 1
        };
        _vk.CreateFramebuffer(_device, in fbCi, null, out _framebuffer);

        var readbackSize = (ulong)(width * height * 4);
        EnsureHostBuffer(ref _readbackBuffer, ref _readbackMemory, ref _readbackMapped, ref _readbackCapacity,
            readbackSize, BufferUsageFlags.TransferDstBit);

        _rtWidth = width;
        _rtHeight = height;
    }

    private Image CreateImage(int width, int height, Format format, ImageUsageFlags usage, out DeviceMemory memory)
    {
        var ci = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = format,
            Extent = new Extent3D((uint)width, (uint)height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined
        };

        memory = default;
        if (_vk.CreateImage(_device, in ci, null, out var image) != Result.Success)
            return default;

        _vk.GetImageMemoryRequirements(_device, image, out var req);
        var ai = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = req.Size,
            MemoryTypeIndex = FindMemoryType(req.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
        };
        if (_vk.AllocateMemory(_device, in ai, null, out memory) != Result.Success)
            return default;

        _vk.BindImageMemory(_device, image, memory, 0);
        return image;
    }

    private ImageView CreateImageView(Image image, Format format, ImageAspectFlags aspect)
    {
        if (image.Handle == 0) return default;
        var ci = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange { AspectMask = aspect, LevelCount = 1, LayerCount = 1 }
        };
        return _vk.CreateImageView(_device, in ci, null, out var view) == Result.Success ? view : default;
    }

    private void EnsureHostBuffer(ref VkBuffer buffer, ref DeviceMemory memory, ref nint mapped,
        ref ulong capacity, ulong needed, BufferUsageFlags usage)
    {
        if (capacity >= needed && mapped != 0) return;

        if (_device.Handle != 0 && _fence.Handle != 0)
        {
            var fence = _fence;
            _vk.WaitForFences(_device, 1, &fence, true, ulong.MaxValue);
        }
        DestroyHostBuffer(ref buffer, ref memory, ref mapped, ref capacity);
        var size = Math.Max(needed * 2, 65536UL);
        var ci = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };
        if (_vk.CreateBuffer(_device, in ci, null, out buffer) != Result.Success) return;

        _vk.GetBufferMemoryRequirements(_device, buffer, out var req);
        var ai = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = req.Size,
            MemoryTypeIndex = FindMemoryType(req.MemoryTypeBits,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit)
        };
        if (_vk.AllocateMemory(_device, in ai, null, out memory) != Result.Success)
        {
            _vk.DestroyBuffer(_device, buffer, null);
            buffer = default;
            return;
        }

        if (_vk.BindBufferMemory(_device, buffer, memory, 0) != Result.Success)
        {
            _vk.DestroyBuffer(_device, buffer, null);
            _vk.FreeMemory(_device, memory, null);
            buffer = default;
            memory = default;
            return;
        }

        void* ptr = null;
        if (_vk.MapMemory(_device, memory, 0, req.Size, 0, &ptr) != Result.Success || ptr == null)
        {
            _vk.DestroyBuffer(_device, buffer, null);
            _vk.FreeMemory(_device, memory, null);
            buffer = default;
            memory = default;
            return;
        }

        mapped = (nint)ptr;
        capacity = size;
    }

    private void DestroyHostBuffer(ref VkBuffer buffer, ref DeviceMemory memory, ref nint mapped, ref ulong capacity)
    {
        if (mapped != 0) { _vk.UnmapMemory(_device, memory); mapped = 0; }
        if (buffer.Handle != 0) { _vk.DestroyBuffer(_device, buffer, null); buffer = default; }
        if (memory.Handle != 0) { _vk.FreeMemory(_device, memory, null); memory = default; }
        capacity = 0;
    }

    private void DestroyRenderTargets()
    {
        if (_framebuffer.Handle != 0) { _vk.DestroyFramebuffer(_device, _framebuffer, null); _framebuffer = default; }
        if (_colorView.Handle != 0) { _vk.DestroyImageView(_device, _colorView, null); _colorView = default; }
        if (_depthView.Handle != 0) { _vk.DestroyImageView(_device, _depthView, null); _depthView = default; }
        if (_colorImage.Handle != 0) { _vk.DestroyImage(_device, _colorImage, null); _colorImage = default; }
        if (_depthImage.Handle != 0) { _vk.DestroyImage(_device, _depthImage, null); _depthImage = default; }
        if (_colorMemory.Handle != 0) { _vk.FreeMemory(_device, _colorMemory, null); _colorMemory = default; }
        if (_depthMemory.Handle != 0) { _vk.FreeMemory(_device, _depthMemory, null); _depthMemory = default; }
        _rtWidth = 0;
        _rtHeight = 0;
    }

    private void RecordAndSubmit(Color clearColor, int width, int height)
    {
        _vk.ResetCommandBuffer(_commandBuffer, 0);
        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };
        _vk.BeginCommandBuffer(_commandBuffer, in beginInfo);

        var clearValues = stackalloc ClearValue[2];
        clearValues[0].Color = new ClearColorValue(clearColor.R / 255f, clearColor.G / 255f, clearColor.B / 255f, clearColor.A / 255f);
        clearValues[1].DepthStencil = new ClearDepthStencilValue(1f, 0);

        var rpBegin = new RenderPassBeginInfo
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _framebuffer,
            RenderArea = new Rect2D { Extent = new Extent2D((uint)width, (uint)height) },
            ClearValueCount = 2,
            PClearValues = clearValues
        };
        _vk.CmdBeginRenderPass(_commandBuffer, in rpBegin, SubpassContents.Inline);

        var vp = new Viewport { X = 0, Y = (float)height, Width = (float)width, Height = -(float)height, MinDepth = 0, MaxDepth = 1 };
        var sc = new Rect2D { Extent = new Extent2D((uint)width, (uint)height) };
        _vk.CmdSetViewport(_commandBuffer, 0, 1, in vp);
        _vk.CmdSetScissor(_commandBuffer, 0, 1, in sc);

        var vb = _vertexBuffer;
        ulong zero = 0;
        _vk.CmdBindVertexBuffers(_commandBuffer, 0, 1, &vb, &zero);
        _vk.CmdBindIndexBuffer(_commandBuffer, _indexBuffer, 0, IndexType.Uint32);

        Pipeline lastPipeline = default;
        foreach (var draw in _draws)
        {
            var pipeline = (draw.Wireframe && _wirePipeline.Handle != 0) ? _wirePipeline : _fillPipeline;
            if (pipeline.Handle != lastPipeline.Handle)
            {
                _vk.CmdBindPipeline(_commandBuffer, PipelineBindPoint.Graphics, pipeline);
                lastPipeline = pipeline;
            }

            var pc = new PushConstantData
            {
                Mvp = draw.Mvp,
                R = draw.R, G = draw.G, B = draw.B, A = draw.A
            };
            _vk.CmdPushConstants(_commandBuffer, _pipelineLayout,
                ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0, 80, &pc);
            _vk.CmdDrawIndexed(_commandBuffer, (uint)draw.IndexCount, 1, (uint)draw.FirstIndex, 0, 0);
        }

        _vk.CmdEndRenderPass(_commandBuffer);

        var copyRegion = new BufferImageCopy
        {
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit,
                LayerCount = 1
            },
            ImageExtent = new Extent3D((uint)width, (uint)height, 1)
        };
        _vk.CmdCopyImageToBuffer(_commandBuffer, _colorImage, ImageLayout.TransferSrcOptimal,
            _readbackBuffer, 1, in copyRegion);

        _vk.EndCommandBuffer(_commandBuffer);

        var cmdBuf = _commandBuffer;
        var submit = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &cmdBuf
        };
        _vk.QueueSubmit(_queue, 1, in submit, _fence);

        var fence = _fence;
        _vk.WaitForFences(_device, 1, &fence, true, ulong.MaxValue);
        _vk.ResetFences(_device, 1, &fence);
    }

    private void ReadBackToSurface(RenderSurface target)
    {
        if (_readbackMapped == 0) return;
        var byteCount = (long)(target.Width * target.Height * sizeof(uint));
        if ((ulong)byteCount > _readbackCapacity) return;
        fixed (uint* dst = target.ColorBuffer)
            System.Buffer.MemoryCopy((void*)_readbackMapped, dst, byteCount, byteCount);
    }

    private void ClearBatch()
    {
        _draws.Clear();
        _vertices.Clear();
        _indices.Clear();
    }

    private uint FindMemoryType(uint typeBits, MemoryPropertyFlags flags)
    {
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var props);
        for (uint i = 0; i < props.MemoryTypeCount; i++)
        {
            if ((typeBits & (1u << (int)i)) != 0 && (props.MemoryTypes[(int)i].PropertyFlags & flags) == flags)
                return i;
        }
        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PushConstantData
    {
        public Matrix4x4 Mvp;
        public float R, G, B, A;
    }

    private readonly record struct DrawCall(
        int FirstIndex, int IndexCount,
        Matrix4x4 Mvp,
        float R, float G, float B, float A,
        bool Wireframe);
}
