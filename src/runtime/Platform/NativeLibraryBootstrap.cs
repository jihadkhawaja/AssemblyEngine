using System.Reflection;
using System.Runtime.InteropServices;

namespace AssemblyEngine.Platform;

internal static class NativeLibraryBootstrap
{
    private static int _initialized;

    public static void EnsureInitialized(Assembly assembly)
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
            return;

        NativeLibrary.SetDllImportResolver(assembly, Resolve);
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        return EnginePlatform.Current.TryLoadNativeLibrary(libraryName, assembly, searchPath, out var handle)
            ? handle
            : IntPtr.Zero;
    }
}