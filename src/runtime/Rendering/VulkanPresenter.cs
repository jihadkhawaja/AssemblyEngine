using Silk.NET.Vulkan;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VkBuffer = Silk.NET.Vulkan.Buffer;
using VkSemaphore = Silk.NET.Vulkan.Semaphore;

namespace AssemblyEngine.Rendering;

[SupportedOSPlatform("windows")]
internal sealed unsafe class VulkanPresenter : IDisposable
{
    public static string? LastFailureReason { get; private set; }

    private readonly Vk _vk;
    private Instance _instance;
    private PhysicalDevice _physicalDevice;
    private Device _device;
    private SurfaceKHR _surface;
    private Queue _queue;
    private CommandPool _commandPool;
    private VkBuffer _stagingBuffer;
    private DeviceMemory _stagingMemory;
    private IntPtr _mappedStaging;
    private ulong _stagingSize;
    private SwapchainKHR _swapchain;
    private SurfaceFormatKHR _surfaceFormat;
    private Image[] _swapchainImages = [];
    private CommandBuffer[] _commandBuffers = [];
    private bool[] _imageInitialized = [];
    private VkSemaphore _imageAvailableSemaphore;
    private VkSemaphore _renderFinishedSemaphore;
    private Fence _inFlightFence;
    private uint _queueFamilyIndex;
    private int _width;
    private int _height;
    private bool _vSyncEnabled;
    private readonly nint _windowHandle;
    private CreateWin32SurfaceKhrDelegate? _createWin32Surface;
    private DestroySurfaceKhrDelegate? _destroySurface;
    private GetPhysicalDeviceSurfaceSupportKhrDelegate? _getPhysicalDeviceSurfaceSupport;
    private GetPhysicalDeviceSurfaceCapabilitiesKhrDelegate? _getPhysicalDeviceSurfaceCapabilities;
    private GetPhysicalDeviceSurfaceFormatsKhrDelegate? _getPhysicalDeviceSurfaceFormats;
    private GetPhysicalDeviceSurfacePresentModesKhrDelegate? _getPhysicalDeviceSurfacePresentModes;
    private CreateSwapchainKhrDelegate? _createSwapchain;
    private DestroySwapchainKhrDelegate? _destroySwapchain;
    private GetSwapchainImagesKhrDelegate? _getSwapchainImages;
    private AcquireNextImageKhrDelegate? _acquireNextImage;
    private QueuePresentKhrDelegate? _queuePresent;

    private VulkanPresenter(Vk vk, nint windowHandle)
    {
        _vk = vk;
        _windowHandle = windowHandle;
    }

    public static VulkanPresenter? TryCreate(nint windowHandle, int width, int height, bool vSyncEnabled)
    {
        LastFailureReason = null;

        if (!OperatingSystem.IsWindows())
        {
            LastFailureReason = "Vulkan presentation is only supported on Windows.";
            return null;
        }

        if (windowHandle == 0 || width <= 0 || height <= 0)
        {
            LastFailureReason = "The native window handle or surface dimensions were invalid.";
            return null;
        }

        try
        {
            var presenter = new VulkanPresenter(Vk.GetApi(), windowHandle);
            if (!presenter.TryInitialize(width, height, vSyncEnabled))
            {
                LastFailureReason ??= "Vulkan initialization did not complete successfully.";
                presenter.Dispose();
                return null;
            }

            return presenter;
        }
        catch (Exception ex)
        {
            LastFailureReason = ex.Message;
            return null;
        }
    }

    public void EnsureSize(int width, int height, bool vSyncEnabled)
    {
        if (width <= 0 || height <= 0 || _device.Handle == 0)
            return;

        if (_width == width && _height == height && _vSyncEnabled == vSyncEnabled && _swapchain.Handle != 0)
            return;

        CreateOrResizeSwapchain(width, height, vSyncEnabled);
    }

    public bool Present(IntPtr sourceBuffer, int width, int height, int stride)
    {
        if (_device.Handle == 0 || _swapchain.Handle == 0 || sourceBuffer == IntPtr.Zero || width <= 0 || height <= 0)
            return false;

        EnsureSize(width, height, _vSyncEnabled);
        if (_swapchain.Handle == 0 || _mappedStaging == IntPtr.Zero)
            return false;

        var sourceSize = checked((ulong)(height * stride));
        if (sourceSize > _stagingSize)
            return false;

        fixed (Fence* fencePtr = &_inFlightFence)
        {
            _vk.WaitForFences(_device, 1, fencePtr, true, ulong.MaxValue);
            _vk.ResetFences(_device, 1, fencePtr);
        }

        global::System.Buffer.MemoryCopy((void*)sourceBuffer, (void*)_mappedStaging, _stagingSize, sourceSize);

        uint imageIndex = 0;
        var acquireResult = _acquireNextImage!(_device, _swapchain, ulong.MaxValue, _imageAvailableSemaphore, default, ref imageIndex);
        if (acquireResult == Result.ErrorOutOfDateKhr)
        {
            CreateOrResizeSwapchain(width, height, _vSyncEnabled);
            return false;
        }

        if (acquireResult != Result.Success && acquireResult != Result.SuboptimalKhr)
            return false;

        RecordCopyCommands(_commandBuffers[imageIndex], _swapchainImages[imageIndex], imageIndex, width, height);

        var waitStage = PipelineStageFlags.TransferBit;
        fixed (VkSemaphore* waitSemaphorePtr = &_imageAvailableSemaphore)
        fixed (VkSemaphore* signalSemaphorePtr = &_renderFinishedSemaphore)
        fixed (CommandBuffer* commandBufferPtr = &_commandBuffers[imageIndex])
        {
            var submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = waitSemaphorePtr,
                PWaitDstStageMask = &waitStage,
                CommandBufferCount = 1,
                PCommandBuffers = commandBufferPtr,
                SignalSemaphoreCount = 1,
                PSignalSemaphores = signalSemaphorePtr
            };

            if (_vk.QueueSubmit(_queue, 1, in submitInfo, _inFlightFence) != Result.Success)
                return false;
        }

        fixed (VkSemaphore* signalSemaphorePtr = &_renderFinishedSemaphore)
        fixed (SwapchainKHR* swapchainPtr = &_swapchain)
        {
            uint* imageIndexPtr = &imageIndex;
            var presentInfo = new PresentInfoKHR
            {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = signalSemaphorePtr,
                SwapchainCount = 1,
                PSwapchains = swapchainPtr,
                PImageIndices = imageIndexPtr
            };

            var presentResult = _queuePresent!(_queue, in presentInfo);
            if (presentResult == Result.ErrorOutOfDateKhr || presentResult == Result.SuboptimalKhr)
            {
                CreateOrResizeSwapchain(width, height, _vSyncEnabled);
                return true;
            }

            return presentResult == Result.Success;
        }
    }

    public void Dispose()
    {
        if (_device.Handle != 0)
            _vk.DeviceWaitIdle(_device);

        DestroySwapchainResources();

        if (_imageAvailableSemaphore.Handle != 0)
            _vk.DestroySemaphore(_device, _imageAvailableSemaphore, null);

        if (_renderFinishedSemaphore.Handle != 0)
            _vk.DestroySemaphore(_device, _renderFinishedSemaphore, null);

        if (_inFlightFence.Handle != 0)
            _vk.DestroyFence(_device, _inFlightFence, null);

        if (_commandPool.Handle != 0)
            _vk.DestroyCommandPool(_device, _commandPool, null);

        if (_device.Handle != 0)
            _vk.DestroyDevice(_device, null);

        if (_surface.Handle != 0)
            _destroySurface?.Invoke(_instance, _surface, null);

        if (_instance.Handle != 0)
            _vk.DestroyInstance(_instance, null);

        _vk.Dispose();
    }

    private bool TryInitialize(int width, int height, bool vSyncEnabled)
    {
        if (!CreateInstance())
            return false;

        if (!LoadExtensionDelegates())
            return false;

        if (!CreateSurface())
            return false;

        if (!PickPhysicalDevice())
            return false;

        if (!CreateDevice())
            return false;

        if (!CreateCommandPool())
            return false;

        if (!CreateSyncObjects())
            return false;

        CreateOrResizeSwapchain(width, height, vSyncEnabled);
        if (_swapchain.Handle == 0)
        {
            LastFailureReason ??= "The Vulkan swapchain could not be created for the window surface.";
            return false;
        }

        return _swapchain.Handle != 0;
    }

    private bool CreateInstance()
    {
        var appName = Marshal.StringToHGlobalAnsi("AssemblyEngine");
        var surfaceExtension = Marshal.StringToHGlobalAnsi("VK_KHR_surface");
        var win32SurfaceExtension = Marshal.StringToHGlobalAnsi("VK_KHR_win32_surface");

        try
        {
            var extensionPointers = stackalloc byte*[2];
            extensionPointers[0] = (byte*)surfaceExtension;
            extensionPointers[1] = (byte*)win32SurfaceExtension;

            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)appName,
                PEngineName = (byte*)appName,
                ApplicationVersion = 1,
                EngineVersion = 1
            };

            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = 2,
                PpEnabledExtensionNames = extensionPointers
            };

            var result = _vk.CreateInstance(in createInfo, null, out _instance);
            if (result != Result.Success)
            {
                LastFailureReason = $"vkCreateInstance failed with {result}.";
                return false;
            }

            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(appName);
            Marshal.FreeHGlobal(surfaceExtension);
            Marshal.FreeHGlobal(win32SurfaceExtension);
        }
    }

    private bool CreateSurface()
    {
        var createInfo = new Win32SurfaceCreateInfoKHR
        {
            SType = StructureType.Win32SurfaceCreateInfoKhr,
            Hwnd = _windowHandle,
            Hinstance = Kernel32.GetModuleHandle(null)
        };

        var result = _createWin32Surface!(_instance, in createInfo, null, out _surface);
        if (result != Result.Success)
        {
            LastFailureReason = $"vkCreateWin32SurfaceKHR failed with {result}.";
            return false;
        }

        return true;
    }

    private bool PickPhysicalDevice()
    {
        uint deviceCount = 0;
        _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, null);
        if (deviceCount == 0)
            return false;

        var devices = new PhysicalDevice[deviceCount];
        fixed (PhysicalDevice* devicesPtr = devices)
            _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, devicesPtr);

        foreach (var device in devices)
        {
            uint queueFamilyCount = 0;
            _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);
            if (queueFamilyCount == 0)
                continue;

            var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
            fixed (QueueFamilyProperties* queueFamilyPtr = queueFamilies)
                _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamilyPtr);

            for (uint queueIndex = 0; queueIndex < queueFamilies.Length; queueIndex++)
            {
                uint surfaceSupported = 0;
                _getPhysicalDeviceSurfaceSupport!(device, queueIndex, _surface, out surfaceSupported);
                if ((queueFamilies[queueIndex].QueueFlags & QueueFlags.GraphicsBit) == 0 || surfaceSupported == 0)
                    continue;

                _physicalDevice = device;
                _queueFamilyIndex = queueIndex;
                return true;
            }
        }

        LastFailureReason = "No Vulkan physical device with graphics and present support was found.";
        return false;
    }

    private bool CreateDevice()
    {
        var queuePriority = 1f;
        var swapchainExtension = Marshal.StringToHGlobalAnsi("VK_KHR_swapchain");

        try
        {
            var queueCreateInfo = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = _queueFamilyIndex,
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };

            var extensionPointers = stackalloc byte*[1];
            extensionPointers[0] = (byte*)swapchainExtension;

            var createInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueCreateInfo,
                EnabledExtensionCount = 1,
                PpEnabledExtensionNames = extensionPointers
            };

            var result = _vk.CreateDevice(_physicalDevice, in createInfo, null, out _device);
            if (result != Result.Success)
            {
                LastFailureReason = $"vkCreateDevice failed with {result}.";
                return false;
            }

            _vk.GetDeviceQueue(_device, _queueFamilyIndex, 0, out _queue);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(swapchainExtension);
        }
    }

    private bool CreateCommandPool()
    {
        var createInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _queueFamilyIndex,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit
        };

        var result = _vk.CreateCommandPool(_device, in createInfo, null, out _commandPool);
        if (result != Result.Success)
        {
            LastFailureReason = $"vkCreateCommandPool failed with {result}.";
            return false;
        }

        return true;
    }

    private bool CreateSyncObjects()
    {
        var semaphoreInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
        var fenceInfo = new FenceCreateInfo
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit
        };

        var imageAvailableResult = _vk.CreateSemaphore(_device, in semaphoreInfo, null, out _imageAvailableSemaphore);
        if (imageAvailableResult != Result.Success)
        {
            LastFailureReason = $"vkCreateSemaphore(imageAvailable) failed with {imageAvailableResult}.";
            return false;
        }

        var renderFinishedResult = _vk.CreateSemaphore(_device, in semaphoreInfo, null, out _renderFinishedSemaphore);
        if (renderFinishedResult != Result.Success)
        {
            LastFailureReason = $"vkCreateSemaphore(renderFinished) failed with {renderFinishedResult}.";
            return false;
        }

        var fenceResult = _vk.CreateFence(_device, in fenceInfo, null, out _inFlightFence);
        if (fenceResult != Result.Success)
        {
            LastFailureReason = $"vkCreateFence failed with {fenceResult}.";
            return false;
        }

        return true;
    }

    private void CreateOrResizeSwapchain(int width, int height, bool vSyncEnabled)
    {
        _vk.DeviceWaitIdle(_device);
        DestroySwapchainResources();

        SurfaceCapabilitiesKHR capabilities = default;
        _getPhysicalDeviceSurfaceCapabilities!(_physicalDevice, _surface, out capabilities);
        if ((capabilities.SupportedUsageFlags & ImageUsageFlags.TransferDstBit) == 0)
        {
            LastFailureReason = "The selected Vulkan surface does not support transfer-destination swapchain images.";
            return;
        }

        var formats = GetSurfaceFormats();
        var presentModes = GetPresentModes();
        _surfaceFormat = SelectSurfaceFormat(formats);
        var presentMode = SelectPresentMode(presentModes, vSyncEnabled);
        var extent = SelectExtent(capabilities, width, height);
        var imageCount = capabilities.MinImageCount + 1;
        if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount)
            imageCount = capabilities.MaxImageCount;

        var createInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            MinImageCount = imageCount,
            ImageFormat = _surfaceFormat.Format,
            ImageColorSpace = _surfaceFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.TransferDstBit,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true,
            OldSwapchain = default
        };

        var createSwapchainResult = _createSwapchain!(_device, in createInfo, null, out _swapchain);
        if (createSwapchainResult != Result.Success)
        {
            LastFailureReason = $"vkCreateSwapchainKHR failed with {createSwapchainResult}.";
            return;
        }

        uint swapchainImageCount = 0;
        _getSwapchainImages!(_device, _swapchain, ref swapchainImageCount, null);
        _swapchainImages = new Image[swapchainImageCount];
        fixed (Image* imagePtr = _swapchainImages)
            _getSwapchainImages!(_device, _swapchain, ref swapchainImageCount, imagePtr);

        _imageInitialized = new bool[swapchainImageCount];
        AllocateCommandBuffers(swapchainImageCount);
        CreateStagingBuffer(extent.Width, extent.Height);
        if (_mappedStaging == IntPtr.Zero)
        {
            LastFailureReason = "The Vulkan staging buffer could not be mapped for CPU writes.";
            DestroySwapchainResources();
            return;
        }

        _width = (int)extent.Width;
        _height = (int)extent.Height;
        _vSyncEnabled = vSyncEnabled;
        LastFailureReason = null;
    }

    private SurfaceFormatKHR[] GetSurfaceFormats()
    {
        uint formatCount = 0;
        _getPhysicalDeviceSurfaceFormats!(_physicalDevice, _surface, ref formatCount, null);
        var formats = new SurfaceFormatKHR[formatCount];
        fixed (SurfaceFormatKHR* formatsPtr = formats)
            _getPhysicalDeviceSurfaceFormats!(_physicalDevice, _surface, ref formatCount, formatsPtr);
        return formats;
    }

    private PresentModeKHR[] GetPresentModes()
    {
        uint presentModeCount = 0;
        _getPhysicalDeviceSurfacePresentModes!(_physicalDevice, _surface, ref presentModeCount, null);
        var presentModes = new PresentModeKHR[presentModeCount];
        fixed (PresentModeKHR* presentModesPtr = presentModes)
            _getPhysicalDeviceSurfacePresentModes!(_physicalDevice, _surface, ref presentModeCount, presentModesPtr);
        return presentModes;
    }

    private static SurfaceFormatKHR SelectSurfaceFormat(SurfaceFormatKHR[] formats)
    {
        foreach (var format in formats)
        {
            if (format.Format == Format.B8G8R8A8Unorm && format.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
                return format;
        }

        return formats.Length > 0 ? formats[0] : default;
    }

    private static PresentModeKHR SelectPresentMode(PresentModeKHR[] presentModes, bool vSyncEnabled)
    {
        if (vSyncEnabled)
            return PresentModeKHR.FifoKhr;

        foreach (var presentMode in presentModes)
        {
            if (presentMode == PresentModeKHR.MailboxKhr)
                return presentMode;

            if (presentMode == PresentModeKHR.ImmediateKhr)
                return presentMode;
        }

        return PresentModeKHR.FifoKhr;
    }

    private static Extent2D SelectExtent(SurfaceCapabilitiesKHR capabilities, int width, int height)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
            return capabilities.CurrentExtent;

        return new Extent2D(
            (uint)Math.Clamp(width, (int)capabilities.MinImageExtent.Width, (int)capabilities.MaxImageExtent.Width),
            (uint)Math.Clamp(height, (int)capabilities.MinImageExtent.Height, (int)capabilities.MaxImageExtent.Height));
    }

    private void AllocateCommandBuffers(uint count)
    {
        if (_commandBuffers.Length > 0)
        {
            fixed (CommandBuffer* commandBufferPtr = _commandBuffers)
                _vk.FreeCommandBuffers(_device, _commandPool, (uint)_commandBuffers.Length, commandBufferPtr);
        }

        _commandBuffers = new CommandBuffer[count];
        fixed (CommandBuffer* commandBufferPtr = _commandBuffers)
        {
            var allocateInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = count
            };

            _vk.AllocateCommandBuffers(_device, in allocateInfo, commandBufferPtr);
        }
    }

    private void CreateStagingBuffer(uint width, uint height)
    {
        if (_mappedStaging != IntPtr.Zero)
        {
            _vk.UnmapMemory(_device, _stagingMemory);
            _mappedStaging = IntPtr.Zero;
        }

        if (_stagingBuffer.Handle != 0)
            _vk.DestroyBuffer(_device, _stagingBuffer, null);

        if (_stagingMemory.Handle != 0)
            _vk.FreeMemory(_device, _stagingMemory, null);

        var createInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = width * height * 4UL,
            Usage = BufferUsageFlags.TransferSrcBit,
            SharingMode = SharingMode.Exclusive
        };

        if (_vk.CreateBuffer(_device, in createInfo, null, out _stagingBuffer) != Result.Success)
            return;

        _vk.GetBufferMemoryRequirements(_device, _stagingBuffer, out var requirements);
        var allocateInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = FindMemoryType(
                requirements.MemoryTypeBits,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit)
        };

        if (_vk.AllocateMemory(_device, in allocateInfo, null, out _stagingMemory) != Result.Success)
            return;

        _vk.BindBufferMemory(_device, _stagingBuffer, _stagingMemory, 0);

        void* mapped = null;
        _vk.MapMemory(_device, _stagingMemory, 0, requirements.Size, 0, &mapped);
        _mappedStaging = (IntPtr)mapped;
        _stagingSize = requirements.Size;
    }

    private void RecordCopyCommands(CommandBuffer commandBuffer, Image image, uint imageIndex, int width, int height)
    {
        _vk.ResetCommandBuffer(commandBuffer, 0);

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };
        _vk.BeginCommandBuffer(commandBuffer, in beginInfo);

        var subresourceRange = new ImageSubresourceRange
        {
            AspectMask = ImageAspectFlags.ColorBit,
            BaseMipLevel = 0,
            LevelCount = 1,
            BaseArrayLayer = 0,
            LayerCount = 1
        };

        var preCopyBarrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = 0,
            DstAccessMask = AccessFlags.TransferWriteBit,
            OldLayout = _imageInitialized[imageIndex] ? ImageLayout.PresentSrcKhr : ImageLayout.Undefined,
            NewLayout = ImageLayout.TransferDstOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = subresourceRange
        };

        _vk.CmdPipelineBarrier(
            commandBuffer,
            _imageInitialized[imageIndex] ? PipelineStageFlags.BottomOfPipeBit : PipelineStageFlags.TopOfPipeBit,
            PipelineStageFlags.TransferBit,
            0,
            0,
            null,
            0,
            null,
            1,
            in preCopyBarrier);

        var copyRegion = new BufferImageCopy
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = new Extent3D((uint)width, (uint)height, 1)
        };

        _vk.CmdCopyBufferToImage(commandBuffer, _stagingBuffer, image, ImageLayout.TransferDstOptimal, 1, in copyRegion);

        var postCopyBarrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.TransferWriteBit,
            DstAccessMask = 0,
            OldLayout = ImageLayout.TransferDstOptimal,
            NewLayout = ImageLayout.PresentSrcKhr,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = subresourceRange
        };

        _vk.CmdPipelineBarrier(
            commandBuffer,
            PipelineStageFlags.TransferBit,
            PipelineStageFlags.BottomOfPipeBit,
            0,
            0,
            null,
            0,
            null,
            1,
            in postCopyBarrier);

        _vk.EndCommandBuffer(commandBuffer);
        _imageInitialized[imageIndex] = true;
    }

    private uint FindMemoryType(uint memoryTypeBits, MemoryPropertyFlags flags)
    {
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memoryProperties);
        for (uint index = 0; index < memoryProperties.MemoryTypeCount; index++)
        {
            if ((memoryTypeBits & (1u << (int)index)) == 0)
                continue;

            if ((memoryProperties.MemoryTypes[(int)index].PropertyFlags & flags) == flags)
                return index;
        }

        throw new InvalidOperationException("No compatible Vulkan memory type was found for the staging buffer.");
    }

    private void DestroySwapchainResources()
    {
        if (_mappedStaging != IntPtr.Zero)
        {
            _vk.UnmapMemory(_device, _stagingMemory);
            _mappedStaging = IntPtr.Zero;
        }

        if (_stagingBuffer.Handle != 0)
        {
            _vk.DestroyBuffer(_device, _stagingBuffer, null);
            _stagingBuffer = default;
        }

        if (_stagingMemory.Handle != 0)
        {
            _vk.FreeMemory(_device, _stagingMemory, null);
            _stagingMemory = default;
        }

        if (_commandBuffers.Length > 0 && _commandPool.Handle != 0)
        {
            fixed (CommandBuffer* commandBufferPtr = _commandBuffers)
                _vk.FreeCommandBuffers(_device, _commandPool, (uint)_commandBuffers.Length, commandBufferPtr);
        }

        _commandBuffers = [];
        _swapchainImages = [];
        _imageInitialized = [];
        _stagingSize = 0;

        if (_swapchain.Handle != 0)
        {
            _destroySwapchain?.Invoke(_device, _swapchain, null);
            _swapchain = default;
        }
    }

    private bool LoadExtensionDelegates()
    {
        _createWin32Surface = LoadInstanceDelegate<CreateWin32SurfaceKhrDelegate>("vkCreateWin32SurfaceKHR");
        _destroySurface = LoadInstanceDelegate<DestroySurfaceKhrDelegate>("vkDestroySurfaceKHR");
        _getPhysicalDeviceSurfaceSupport = LoadInstanceDelegate<GetPhysicalDeviceSurfaceSupportKhrDelegate>("vkGetPhysicalDeviceSurfaceSupportKHR");
        _getPhysicalDeviceSurfaceCapabilities = LoadInstanceDelegate<GetPhysicalDeviceSurfaceCapabilitiesKhrDelegate>("vkGetPhysicalDeviceSurfaceCapabilitiesKHR");
        _getPhysicalDeviceSurfaceFormats = LoadInstanceDelegate<GetPhysicalDeviceSurfaceFormatsKhrDelegate>("vkGetPhysicalDeviceSurfaceFormatsKHR");
        _getPhysicalDeviceSurfacePresentModes = LoadInstanceDelegate<GetPhysicalDeviceSurfacePresentModesKhrDelegate>("vkGetPhysicalDeviceSurfacePresentModesKHR");
        _createSwapchain = LoadDeviceDelegate<CreateSwapchainKhrDelegate>("vkCreateSwapchainKHR");
        _destroySwapchain = LoadDeviceDelegate<DestroySwapchainKhrDelegate>("vkDestroySwapchainKHR");
        _getSwapchainImages = LoadDeviceDelegate<GetSwapchainImagesKhrDelegate>("vkGetSwapchainImagesKHR");
        _acquireNextImage = LoadDeviceDelegate<AcquireNextImageKhrDelegate>("vkAcquireNextImageKHR");
        _queuePresent = LoadDeviceDelegate<QueuePresentKhrDelegate>("vkQueuePresentKHR");

        var missingDelegates = new List<string>(11);
        if (_createWin32Surface is null)
            missingDelegates.Add("vkCreateWin32SurfaceKHR");

        if (_destroySurface is null)
            missingDelegates.Add("vkDestroySurfaceKHR");

        if (_getPhysicalDeviceSurfaceSupport is null)
            missingDelegates.Add("vkGetPhysicalDeviceSurfaceSupportKHR");

        if (_getPhysicalDeviceSurfaceCapabilities is null)
            missingDelegates.Add("vkGetPhysicalDeviceSurfaceCapabilitiesKHR");

        if (_getPhysicalDeviceSurfaceFormats is null)
            missingDelegates.Add("vkGetPhysicalDeviceSurfaceFormatsKHR");

        if (_getPhysicalDeviceSurfacePresentModes is null)
            missingDelegates.Add("vkGetPhysicalDeviceSurfacePresentModesKHR");

        if (_createSwapchain is null)
            missingDelegates.Add("vkCreateSwapchainKHR");

        if (_destroySwapchain is null)
            missingDelegates.Add("vkDestroySwapchainKHR");

        if (_getSwapchainImages is null)
            missingDelegates.Add("vkGetSwapchainImagesKHR");

        if (_acquireNextImage is null)
            missingDelegates.Add("vkAcquireNextImageKHR");

        if (_queuePresent is null)
            missingDelegates.Add("vkQueuePresentKHR");

        if (missingDelegates.Count == 0)
            return true;

        LastFailureReason = $"Required Vulkan entry points were unavailable: {string.Join(", ", missingDelegates)}.";
        return false;
    }

    private T? LoadInstanceDelegate<T>(string name) where T : Delegate
    {
        var functionPointer = VulkanNative.GetInstanceProcAddr(_instance, name);
        return functionPointer == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(functionPointer);
    }

    private T? LoadDeviceDelegate<T>(string name) where T : Delegate
    {
        var functionPointer = VulkanNative.GetDeviceProcAddr(_device, name);
        if (functionPointer == IntPtr.Zero)
            functionPointer = VulkanNative.GetInstanceProcAddr(_instance, name);
        return functionPointer == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(functionPointer);
    }

    private static class Kernel32
    {
        [DllImport("kernel32", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
        internal static extern nint GetModuleHandle(string? moduleName);
    }

    private static class VulkanNative
    {
        [DllImport("vulkan-1", EntryPoint = "vkGetInstanceProcAddr", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Winapi)]
        private static extern unsafe nint VkGetInstanceProcAddr(Instance instance, byte* name);

        [DllImport("vulkan-1", EntryPoint = "vkGetDeviceProcAddr", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Winapi)]
        private static extern unsafe nint VkGetDeviceProcAddr(Device device, byte* name);

        internal static unsafe nint GetInstanceProcAddr(Instance instance, string name)
        {
            var namePtr = Marshal.StringToHGlobalAnsi(name);
            try
            {
                return VkGetInstanceProcAddr(instance, (byte*)namePtr);
            }
            finally
            {
                Marshal.FreeHGlobal(namePtr);
            }
        }

        internal static unsafe nint GetDeviceProcAddr(Device device, string name)
        {
            var namePtr = Marshal.StringToHGlobalAnsi(name);
            try
            {
                return VkGetDeviceProcAddr(device, (byte*)namePtr);
            }
            finally
            {
                Marshal.FreeHGlobal(namePtr);
            }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate Result CreateWin32SurfaceKhrDelegate(Instance instance, in Win32SurfaceCreateInfoKHR createInfo, void* allocator, out SurfaceKHR surface);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void DestroySurfaceKhrDelegate(Instance instance, SurfaceKHR surface, void* allocator);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate Result GetPhysicalDeviceSurfaceSupportKhrDelegate(PhysicalDevice physicalDevice, uint queueFamilyIndex, SurfaceKHR surface, out uint supported);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate Result GetPhysicalDeviceSurfaceCapabilitiesKhrDelegate(PhysicalDevice physicalDevice, SurfaceKHR surface, out SurfaceCapabilitiesKHR capabilities);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate Result GetPhysicalDeviceSurfaceFormatsKhrDelegate(PhysicalDevice physicalDevice, SurfaceKHR surface, ref uint formatCount, SurfaceFormatKHR* formats);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate Result GetPhysicalDeviceSurfacePresentModesKhrDelegate(PhysicalDevice physicalDevice, SurfaceKHR surface, ref uint presentModeCount, PresentModeKHR* presentModes);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate Result CreateSwapchainKhrDelegate(Device device, in SwapchainCreateInfoKHR createInfo, void* allocator, out SwapchainKHR swapchain);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void DestroySwapchainKhrDelegate(Device device, SwapchainKHR swapchain, void* allocator);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate Result GetSwapchainImagesKhrDelegate(Device device, SwapchainKHR swapchain, ref uint imageCount, Image* images);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate Result AcquireNextImageKhrDelegate(Device device, SwapchainKHR swapchain, ulong timeout, VkSemaphore semaphore, Fence fence, ref uint imageIndex);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate Result QueuePresentKhrDelegate(Queue queue, in PresentInfoKHR presentInfo);
}