using AssemblyEngine.Core;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AssemblyEngine.Platform;

internal interface IEnginePlatform
{
    Exception CreateNativeCoreLoadException(Exception innerException);
    int ToNativeKeyCode(KeyCode keyCode);
    int ToNativeMouseButton(MouseButton mouseButton);
    bool TryLoadNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath, out IntPtr handle);
}