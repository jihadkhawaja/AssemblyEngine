using System.Runtime.InteropServices;

namespace AssemblyEngine.Platform;

internal static class EnginePlatform
{
    public static IEnginePlatform Current { get; } = CreateCurrent();

    private static IEnginePlatform CreateCurrent()
    {
        if (OperatingSystem.IsWindows())
            return new Windows.WindowsEnginePlatform();

        return new UnsupportedEnginePlatform(RuntimeInformation.OSDescription);
    }
}