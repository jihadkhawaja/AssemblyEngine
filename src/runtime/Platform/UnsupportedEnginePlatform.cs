using AssemblyEngine.Core;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AssemblyEngine.Platform;

internal sealed class UnsupportedEnginePlatform(string osDescription) : IEnginePlatform
{
    private readonly string _osDescription = string.IsNullOrWhiteSpace(osDescription)
        ? "this operating system"
        : osDescription;

    public Exception CreateNativeCoreLoadException(Exception innerException)
    {
        return CreateNotSupportedException(innerException);
    }

    public int ToNativeKeyCode(KeyCode keyCode)
    {
        throw CreateNotSupportedException();
    }

    public int ToNativeMouseButton(MouseButton mouseButton)
    {
        throw CreateNotSupportedException();
    }

    public bool TryLoadNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath, out IntPtr handle)
    {
        handle = IntPtr.Zero;
        return false;
    }

    private PlatformNotSupportedException CreateNotSupportedException(Exception? innerException = null)
    {
        return new PlatformNotSupportedException(
            $"AssemblyEngine does not have a native backend for {_osDescription} yet. Keep OS-specific integration behind the runtime platform layer and add a matching native backend before running the engine on this platform.",
            innerException);
    }
}