using AssemblyEngine.Core;
using AssemblyEngine.Diagnostics;
using AssemblyEngine.Interop;
using System.Numerics;
using System.Runtime.Versioning;

namespace AssemblyEngine.Rendering;

[SupportedOSPlatform("windows")]
internal sealed class UnifiedRenderer : IDisposable
{
    private readonly RenderSurface _surface = new();
    private readonly TextureStore _textures = new();
    private readonly Camera3D _defaultCamera = new();
    private readonly SoftwareWindowPresenter _softwarePresenter = new();
    private VulkanPresenter? _vulkanPresenter;
    private VulkanMeshRenderer? _meshRenderer;
    private nint _windowHandle;
    private bool _vSyncEnabled = true;
    private GraphicsBackend _preferredBackend = GraphicsBackend.Software;
    private bool _reportedVulkanFallback;
    private bool _reportedMeshRendererFallback;
    private Color _clearColor;

    public GraphicsBackend Backend { get; private set; } = GraphicsBackend.Software;
    public Camera3D? ActiveCamera { get; set; }

    public void BeginFrame(int width, int height)
    {
        _surface.Resize(width, height);

        if (_preferredBackend == GraphicsBackend.Vulkan)
        {
            EnsureWindowBinding();
            _vulkanPresenter ??= VulkanPresenter.TryCreate(_windowHandle, width, height, _vSyncEnabled);
            ReportVulkanFallbackIfNeeded();
            _vulkanPresenter?.EnsureSize(width, height, _vSyncEnabled);

            if (_meshRenderer is null && !_reportedMeshRendererFallback)
            {
                _meshRenderer = VulkanMeshRenderer.TryCreate();
                if (_meshRenderer is null)
                {
                    _reportedMeshRendererFallback = true;
                    RuntimeDiagnosticsBridge.Current.LogWarning("engine.render",
                        "Vulkan mesh renderer unavailable, 3D rendering will use software.");
                    Console.Error.WriteLine("AssemblyEngine: Vulkan mesh renderer unavailable, 3D falls back to software.");
                }
                else
                {
                    RuntimeDiagnosticsBridge.Current.LogInfo("engine.render", "Vulkan mesh renderer initialized.");
                }
            }
        }
    }

    public void SetVSyncEnabled(bool enabled)
    {
        _vSyncEnabled = enabled;
        _softwarePresenter.SetVSyncEnabled(enabled);
        _vulkanPresenter?.EnsureSize(_surface.Width, _surface.Height, _vSyncEnabled);
    }

    public void SetPreferredBackend(GraphicsBackend preferredBackend)
    {
        _preferredBackend = preferredBackend;
        _reportedVulkanFallback = false;
        _reportedMeshRendererFallback = false;
        if (preferredBackend != GraphicsBackend.Vulkan)
        {
            _vulkanPresenter?.Dispose();
            _vulkanPresenter = null;
            _meshRenderer?.Dispose();
            _meshRenderer = null;
            Backend = GraphicsBackend.Software;
        }
    }

    public void Clear(Color color)
    {
        _clearColor = color;
        _surface.Clear(color);
    }

    public void DrawPixel(int x, int y, Color color)
    {
        FlushGpuBatch();
        SoftwareRasterizer2D.DrawPixel(_surface, x, y, color);
    }

    public void DrawRect(int x, int y, int width, int height, Color color)
    {
        FlushGpuBatch();
        SoftwareRasterizer2D.DrawRect(_surface, x, y, width, height, color);
    }

    public void DrawFilledRect(int x, int y, int width, int height, Color color)
    {
        FlushGpuBatch();
        SoftwareRasterizer2D.DrawFilledRect(_surface, x, y, width, height, color);
    }

    public void DrawLine(int x1, int y1, int x2, int y2, Color color)
    {
        FlushGpuBatch();
        SoftwareRasterizer2D.DrawLine(_surface, x1, y1, x2, y2, color);
    }

    public void DrawCircle(int cx, int cy, int radius, Color color)
    {
        FlushGpuBatch();
        SoftwareRasterizer2D.DrawCircle(_surface, cx, cy, radius, color);
    }

    public int LoadTexture(string path) => _textures.Load(path);

    public void DrawSprite(int id, int x, int y, bool alphaBlend)
    {
        var texture = _textures.Get(id);
        if (texture is null)
            return;

        FlushGpuBatch();
        SoftwareRasterizer2D.DrawSprite(_surface, texture, x, y, alphaBlend);
    }

    public void DrawMesh(Mesh mesh, Matrix4x4 transform, Color color, bool wireframe)
    {
        var camera = ActiveCamera ?? _defaultCamera;
        if (_meshRenderer is not null)
        {
            var aspect = _surface.Height == 0 ? 1f : (float)_surface.Width / _surface.Height;
            var mvp = transform * camera.CreateViewMatrix() * camera.CreateProjectionMatrix(aspect);
            _meshRenderer.QueueDraw(mesh, mvp, color, wireframe);
            return;
        }

        SoftwareRasterizer3D.DrawMesh(_surface, mesh, transform, camera, color, wireframe);
    }

    public unsafe void Present()
    {
        FlushGpuBatch();

        fixed (uint* colorBuffer = _surface.ColorBuffer)
        {
            NativeCore.UploadFramebuffer((byte*)colorBuffer, _surface.ByteLength);

            if (_preferredBackend == GraphicsBackend.Vulkan
                && _vulkanPresenter is not null
                && _vulkanPresenter.Present((IntPtr)colorBuffer, _surface.Width, _surface.Height, _surface.Stride))
            {
                Backend = _meshRenderer is not null ? GraphicsBackend.Vulkan : GraphicsBackend.Software;
                return;
            }

            Backend = _meshRenderer is not null ? GraphicsBackend.Vulkan : GraphicsBackend.Software;
            ReportVulkanFallbackIfNeeded();
            if (_softwarePresenter.Present((IntPtr)colorBuffer, _surface.Width, _surface.Height, _surface.Stride))
                return;

            NativeCore.Present();
        }
    }

    public void Shutdown()
    {
        _softwarePresenter.Dispose();
        _vulkanPresenter?.Dispose();
        _vulkanPresenter = null;
        _meshRenderer?.Dispose();
        _meshRenderer = null;
        _windowHandle = 0;
        ActiveCamera = null;
        Backend = GraphicsBackend.Software;
    }

    public void Dispose()
    {
        Shutdown();
        GC.SuppressFinalize(this);
    }

    private void FlushGpuBatch()
    {
        if (_meshRenderer is null || !_meshRenderer.HasPendingDraws)
            return;

        _meshRenderer.Flush(_clearColor, _surface);
    }

    private void EnsureWindowBinding()
    {
        if (_windowHandle != 0)
            return;

        _windowHandle = NativeCore.GetWindowHandle();
    }

    private void ReportVulkanFallbackIfNeeded()
    {
        if (_preferredBackend != GraphicsBackend.Vulkan || _vulkanPresenter is not null || _reportedVulkanFallback)
            return;

        _reportedVulkanFallback = true;
        var reason = VulkanPresenter.LastFailureReason ?? "Unknown Vulkan initialization failure.";
        RuntimeDiagnosticsBridge.Current.LogWarning("engine.render",
            $"Vulkan presentation unavailable, falling back to GDI: {reason}");
        Console.Error.WriteLine($"AssemblyEngine: Vulkan presentation unavailable, falling back to GDI. {reason}");
    }
}