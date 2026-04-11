using AssemblyEngine.Platform;

namespace AssemblyEngine.Core;

/// <summary>
/// Frame timing information.
/// </summary>
public static class Time
{
    public static float DeltaTime => EngineHost.DeltaTime;
    public static int Fps => EngineHost.Fps;
    public static long Ticks => EngineHost.Ticks;
}
