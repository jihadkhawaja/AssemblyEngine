using AssemblyEngine.Core;

namespace FpsSample;

public sealed class FpsSampleSettings
{
    public int Width { get; set; } = 1024;

    public int Height { get; set; } = 640;

    public WindowMode WindowMode { get; set; } = WindowMode.Windowed;

    public bool VSyncEnabled { get; set; } = true;

    public float UiScale { get; set; } = 1f;

    public GraphicsBackend PresentationBackend { get; set; } = GraphicsBackend.Vulkan;

    public void Sanitize()
    {
        Width = Math.Clamp(Width, 960, 3840);
        Height = Math.Clamp(Height, 640, 2160);
        UiScale = Math.Clamp(UiScale, 0.75f, 2f);
        if (!Enum.IsDefined(WindowMode))
            WindowMode = WindowMode.Windowed;
        if (!Enum.IsDefined(PresentationBackend))
            PresentationBackend = GraphicsBackend.Software;
    }
}