using AssemblyEngine.Interop;

namespace AssemblyEngine.Core;

/// <summary>
/// Frame timing information from the native performance counter.
/// </summary>
public static class Time
{
    public static float DeltaTime
    {
        get
        {
            int bits = NativeCore.GetDeltaTimeBits();
            return BitConverter.Int32BitsToSingle(bits);
        }
    }

    public static int Fps => NativeCore.GetFps();
    public static long Ticks => NativeCore.GetTicks();
}
