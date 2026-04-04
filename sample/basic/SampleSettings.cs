namespace SampleGame;

using AssemblyEngine.Core;

public sealed class SampleSettings
{
    public int Width { get; set; } = 800;
    public int Height { get; set; } = 600;
    public WindowMode WindowMode { get; set; } = WindowMode.Windowed;
    public bool VSyncEnabled { get; set; } = true;
    public float UiScale { get; set; } = 1f;

    public void Sanitize()
    {
        Width = Math.Clamp(Width, 800, 3840);
        Height = Math.Clamp(Height, 600, 2160);
        UiScale = Math.Clamp(UiScale, 0.75f, 2f);
        if (!Enum.IsDefined(WindowMode))
            WindowMode = WindowMode.Windowed;
    }
}