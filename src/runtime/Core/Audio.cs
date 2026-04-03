using AssemblyEngine.Interop;

namespace AssemblyEngine.Core;

/// <summary>
/// High-level audio API wrapping the native assembly audio system.
/// </summary>
public static class Audio
{
    public static int LoadSound(string path) => NativeCore.LoadSound(path);
    public static bool PlaySound(int id) => NativeCore.PlaySound(id) != 0;
    public static void StopAll() => NativeCore.StopSound();
}
