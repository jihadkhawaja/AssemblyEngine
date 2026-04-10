using AssemblyEngine.Core;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AssemblyEngine.Platform.Windows;

internal sealed class WindowsEnginePlatform : IEnginePlatform
{
    public Exception CreateNativeCoreLoadException(Exception innerException)
    {
        var processArchitecture = RuntimeInformation.ProcessArchitecture;
        var runtimeIdentifier = processArchitecture switch
        {
            Architecture.Arm64 => "win-arm64",
            Architecture.X64 => "win-x64",
            _ => null
        };

        var targetHint = runtimeIdentifier is null
            ? "Build and run a Windows target that matches the current process architecture."
            : $"Build and run {runtimeIdentifier} or copy the matching assemblycore library beside the executable.";

        return new PlatformNotSupportedException(
            $"AssemblyEngine could not load a Windows native core for the current process architecture ({processArchitecture}). {targetHint}",
            innerException);
    }

    public int ToNativeKeyCode(KeyCode keyCode)
    {
        return keyCode switch
        {
            KeyCode.None => 0x00,
            KeyCode.Escape => 0x1B,
            KeyCode.Space => 0x20,
            KeyCode.Left => 0x25,
            KeyCode.Up => 0x26,
            KeyCode.Right => 0x27,
            KeyCode.Down => 0x28,
            KeyCode.A => 0x41,
            KeyCode.B => 0x42,
            KeyCode.C => 0x43,
            KeyCode.D => 0x44,
            KeyCode.E => 0x45,
            KeyCode.F => 0x46,
            KeyCode.G => 0x47,
            KeyCode.H => 0x48,
            KeyCode.I => 0x49,
            KeyCode.J => 0x4A,
            KeyCode.K => 0x4B,
            KeyCode.L => 0x4C,
            KeyCode.M => 0x4D,
            KeyCode.N => 0x4E,
            KeyCode.O => 0x4F,
            KeyCode.P => 0x50,
            KeyCode.Q => 0x51,
            KeyCode.R => 0x52,
            KeyCode.S => 0x53,
            KeyCode.T => 0x54,
            KeyCode.U => 0x55,
            KeyCode.V => 0x56,
            KeyCode.W => 0x57,
            KeyCode.X => 0x58,
            KeyCode.Y => 0x59,
            KeyCode.Z => 0x5A,
            KeyCode.D0 => 0x30,
            KeyCode.D1 => 0x31,
            KeyCode.D2 => 0x32,
            KeyCode.D3 => 0x33,
            KeyCode.D4 => 0x34,
            KeyCode.D5 => 0x35,
            KeyCode.D6 => 0x36,
            KeyCode.D7 => 0x37,
            KeyCode.D8 => 0x38,
            KeyCode.D9 => 0x39,
            KeyCode.F1 => 0x70,
            KeyCode.F2 => 0x71,
            KeyCode.F3 => 0x72,
            KeyCode.F4 => 0x73,
            KeyCode.F5 => 0x74,
            KeyCode.F6 => 0x75,
            KeyCode.F7 => 0x76,
            KeyCode.F8 => 0x77,
            KeyCode.F9 => 0x78,
            KeyCode.F10 => 0x79,
            KeyCode.F11 => 0x7A,
            KeyCode.F12 => 0x7B,
            KeyCode.Enter => 0x0D,
            KeyCode.Tab => 0x09,
            KeyCode.Shift => 0x10,
            KeyCode.Control => 0x11,
            KeyCode.Alt => 0x12,
            KeyCode.BackSpace => 0x08,
            KeyCode.Delete => 0x2E,
            KeyCode.Home => 0x24,
            KeyCode.End => 0x23,
            KeyCode.PageUp => 0x21,
            KeyCode.PageDown => 0x22,
            KeyCode.Insert => 0x2D,
            KeyCode.OemPeriod => 0xBE,
            _ => throw new ArgumentOutOfRangeException(nameof(keyCode), keyCode, "Unknown engine key code.")
        };
    }

    public int ToNativeMouseButton(MouseButton mouseButton)
    {
        return mouseButton switch
        {
            MouseButton.Left => 0,
            MouseButton.Right => 1,
            MouseButton.Middle => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(mouseButton), mouseButton, "Unknown engine mouse button.")
        };
    }

    public bool TryLoadNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath, out IntPtr handle)
    {
        handle = IntPtr.Zero;

        if (!libraryName.Equals("assemblycore", StringComparison.OrdinalIgnoreCase) &&
            !libraryName.Equals("assemblycore.dll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var candidate in GetNativeLibraryCandidates())
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out handle))
                return true;
        }

        return NativeLibrary.TryLoad("assemblycore.dll", assembly, searchPath, out handle);
    }

    private static IEnumerable<string> GetNativeLibraryCandidates()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var architectureFolder = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            _ => string.Empty
        };

        yield return Path.Combine(baseDirectory, "assemblycore.dll");

        if (!string.IsNullOrEmpty(architectureFolder))
            yield return Path.Combine(baseDirectory, "native", architectureFolder, "assemblycore.dll");
    }
}