using AssemblyEngine.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SilkKey = Silk.NET.Input.Key;
using SilkMouseButton = Silk.NET.Input.MouseButton;

namespace AssemblyEngine.Platform;

internal static class EngineHost
{
    private static IWindow? _window;
    private static IInputContext? _input;
    private static IKeyboard? _keyboard;
    private static IMouse? _mouse;
    private static readonly Stopwatch _stopwatch = new();
    private static float _deltaTime;
    private static int _fps;
    private static int _fpsFrameCount;
    private static float _fpsAccumulator;

    private static readonly HashSet<SilkKey> _keysJustPressed = [];
    private static readonly Dictionary<SilkKey, bool> _injectedKeys = [];
    private static readonly Dictionary<SilkMouseButton, bool> _injectedMouseButtons = [];
    private static int? _injectedMouseX;
    private static int? _injectedMouseY;

    public static int WindowWidth => _window?.Size.X ?? 0;
    public static int WindowHeight => _window?.Size.Y ?? 0;
    public static nint WindowHandle { get; private set; }
    public static float DeltaTime => _deltaTime;
    public static int Fps => _fps;
    public static long Ticks => _stopwatch.ElapsedTicks;

    public static void Initialize(int width, int height, string title)
    {
        // GLFW sets the process to per-monitor DPI aware, which changes the
        // window's apparent size on high-DPI displays. The old native core
        // was DPI-unaware, so Windows scaled the window up automatically.
        // Set DPI-unaware before GLFW initializes to preserve that behavior.
        SetDpiUnaware();

        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(width, height),
            Title = title,
            API = GraphicsAPI.None,
            VSync = false,
        };

        _window = Window.Create(options);
        _window.Initialize();

        var win32 = _window.Native?.Win32;
        if (win32.HasValue)
            WindowHandle = win32.Value.Hwnd;

        _input = _window.CreateInput();
        if (_input.Keyboards.Count > 0)
        {
            _keyboard = _input.Keyboards[0];
            _keyboard.KeyDown += OnKeyDown;
        }

        if (_input.Mice.Count > 0)
            _mouse = _input.Mice[0];

        _stopwatch.Restart();
    }

    public static bool PollEvents()
    {
        if (_window is null || _window.IsClosing)
            return false;

        var elapsed = _stopwatch.Elapsed.TotalSeconds;
        _stopwatch.Restart();
        _deltaTime = (float)elapsed;

        _fpsAccumulator += _deltaTime;
        _fpsFrameCount++;
        if (_fpsAccumulator >= 1.0f)
        {
            _fps = _fpsFrameCount;
            _fpsFrameCount = 0;
            _fpsAccumulator -= 1.0f;
        }

        _keysJustPressed.Clear();
        _window.DoEvents();
        return !_window.IsClosing;
    }

    public static void Shutdown()
    {
        _input?.Dispose();
        _input = null;
        _keyboard = null;
        _mouse = null;

        if (_window is not null)
        {
            _window.Close();
            _window.Dispose();
            _window = null;
        }

        WindowHandle = 0;
        _stopwatch.Stop();
        ClearInjectedInput();
    }

    public static bool ResizeWindow(int width, int height)
    {
        if (_window is null) return false;
        _window.Size = new Vector2D<int>(width, height);
        return true;
    }

    public static bool SetWindowMode(WindowMode mode)
    {
        if (_window is null) return false;

        switch (mode)
        {
            case WindowMode.Windowed:
                _window.WindowBorder = WindowBorder.Resizable;
                _window.WindowState = WindowState.Normal;
                break;
            case WindowMode.MaximizedWindow:
                _window.WindowBorder = WindowBorder.Resizable;
                _window.WindowState = WindowState.Maximized;
                break;
            case WindowMode.BorderlessFullscreen:
                _window.WindowBorder = WindowBorder.Hidden;
                _window.WindowState = WindowState.Fullscreen;
                break;
            default:
                return false;
        }

        return true;
    }

    public static WindowMode GetWindowMode()
    {
        if (_window is null) return WindowMode.Windowed;

        return _window.WindowState switch
        {
            WindowState.Fullscreen => WindowMode.BorderlessFullscreen,
            WindowState.Maximized => WindowMode.MaximizedWindow,
            _ => WindowMode.Windowed,
        };
    }

    // --- Input Queries ---

    public static bool IsKeyDown(KeyCode key)
    {
        if (key == KeyCode.Shift)
            return CheckKeyDown(SilkKey.ShiftLeft) || CheckKeyDown(SilkKey.ShiftRight);
        if (key == KeyCode.Control)
            return CheckKeyDown(SilkKey.ControlLeft) || CheckKeyDown(SilkKey.ControlRight);
        if (key == KeyCode.Alt)
            return CheckKeyDown(SilkKey.AltLeft) || CheckKeyDown(SilkKey.AltRight);

        return CheckKeyDown(MapKey(key));
    }

    public static bool IsKeyPressed(KeyCode key)
    {
        if (key == KeyCode.Shift)
            return _keysJustPressed.Contains(SilkKey.ShiftLeft) || _keysJustPressed.Contains(SilkKey.ShiftRight);
        if (key == KeyCode.Control)
            return _keysJustPressed.Contains(SilkKey.ControlLeft) || _keysJustPressed.Contains(SilkKey.ControlRight);
        if (key == KeyCode.Alt)
            return _keysJustPressed.Contains(SilkKey.AltLeft) || _keysJustPressed.Contains(SilkKey.AltRight);

        return _keysJustPressed.Contains(MapKey(key));
    }

    public static int MouseX => _injectedMouseX ?? (int)(_mouse?.Position.X ?? 0);
    public static int MouseY => _injectedMouseY ?? (int)(_mouse?.Position.Y ?? 0);

    public static bool IsMouseDown(Core.MouseButton button)
    {
        var silkButton = MapMouseButton(button);
        if (_injectedMouseButtons.TryGetValue(silkButton, out var injected) && injected)
            return true;
        return _mouse?.IsButtonPressed(silkButton) == true;
    }

    // --- Injected Input (diagnostics / MCP) ---

    public static void InjectKeyState(KeyCode key, bool isDown)
    {
        var silkKey = MapKey(key);
        var wasDown = _injectedKeys.TryGetValue(silkKey, out var prev) && prev;
        _injectedKeys[silkKey] = isDown;
        if (isDown && !wasDown)
            _keysJustPressed.Add(silkKey);
    }

    public static void InjectMousePosition(int x, int y)
    {
        _injectedMouseX = x;
        _injectedMouseY = y;
    }

    public static void InjectMouseButtonState(Core.MouseButton button, bool isDown)
    {
        _injectedMouseButtons[MapMouseButton(button)] = isDown;
    }

    public static void ClearInjectedInput()
    {
        _injectedKeys.Clear();
        _injectedMouseButtons.Clear();
        _injectedMouseX = null;
        _injectedMouseY = null;
    }

    // --- Internal ---

    private static bool CheckKeyDown(SilkKey silkKey)
    {
        if (_keyboard?.IsKeyPressed(silkKey) == true)
            return true;
        return _injectedKeys.TryGetValue(silkKey, out var injected) && injected;
    }

    private static void OnKeyDown(IKeyboard keyboard, SilkKey key, int scancode)
    {
        _keysJustPressed.Add(key);
    }

    private static SilkKey MapKey(KeyCode key) => key switch
    {
        KeyCode.Escape => SilkKey.Escape,
        KeyCode.Space => SilkKey.Space,
        KeyCode.Left => SilkKey.Left,
        KeyCode.Up => SilkKey.Up,
        KeyCode.Right => SilkKey.Right,
        KeyCode.Down => SilkKey.Down,
        KeyCode.A => SilkKey.A,
        KeyCode.B => SilkKey.B,
        KeyCode.C => SilkKey.C,
        KeyCode.D => SilkKey.D,
        KeyCode.E => SilkKey.E,
        KeyCode.F => SilkKey.F,
        KeyCode.G => SilkKey.G,
        KeyCode.H => SilkKey.H,
        KeyCode.I => SilkKey.I,
        KeyCode.J => SilkKey.J,
        KeyCode.K => SilkKey.K,
        KeyCode.L => SilkKey.L,
        KeyCode.M => SilkKey.M,
        KeyCode.N => SilkKey.N,
        KeyCode.O => SilkKey.O,
        KeyCode.P => SilkKey.P,
        KeyCode.Q => SilkKey.Q,
        KeyCode.R => SilkKey.R,
        KeyCode.S => SilkKey.S,
        KeyCode.T => SilkKey.T,
        KeyCode.U => SilkKey.U,
        KeyCode.V => SilkKey.V,
        KeyCode.W => SilkKey.W,
        KeyCode.X => SilkKey.X,
        KeyCode.Y => SilkKey.Y,
        KeyCode.Z => SilkKey.Z,
        KeyCode.D0 => SilkKey.Number0,
        KeyCode.D1 => SilkKey.Number1,
        KeyCode.D2 => SilkKey.Number2,
        KeyCode.D3 => SilkKey.Number3,
        KeyCode.D4 => SilkKey.Number4,
        KeyCode.D5 => SilkKey.Number5,
        KeyCode.D6 => SilkKey.Number6,
        KeyCode.D7 => SilkKey.Number7,
        KeyCode.D8 => SilkKey.Number8,
        KeyCode.D9 => SilkKey.Number9,
        KeyCode.F1 => SilkKey.F1,
        KeyCode.F2 => SilkKey.F2,
        KeyCode.F3 => SilkKey.F3,
        KeyCode.F4 => SilkKey.F4,
        KeyCode.F5 => SilkKey.F5,
        KeyCode.F6 => SilkKey.F6,
        KeyCode.F7 => SilkKey.F7,
        KeyCode.F8 => SilkKey.F8,
        KeyCode.F9 => SilkKey.F9,
        KeyCode.F10 => SilkKey.F10,
        KeyCode.F11 => SilkKey.F11,
        KeyCode.F12 => SilkKey.F12,
        KeyCode.Enter => SilkKey.Enter,
        KeyCode.Tab => SilkKey.Tab,
        KeyCode.Shift => SilkKey.ShiftLeft,
        KeyCode.Control => SilkKey.ControlLeft,
        KeyCode.Alt => SilkKey.AltLeft,
        KeyCode.BackSpace => SilkKey.Backspace,
        KeyCode.Delete => SilkKey.Delete,
        KeyCode.Home => SilkKey.Home,
        KeyCode.End => SilkKey.End,
        KeyCode.PageUp => SilkKey.PageUp,
        KeyCode.PageDown => SilkKey.PageDown,
        KeyCode.Insert => SilkKey.Insert,
        KeyCode.OemPeriod => SilkKey.Period,
        _ => SilkKey.Unknown,
    };

    private static SilkMouseButton MapMouseButton(Core.MouseButton button) => button switch
    {
        Core.MouseButton.Left => SilkMouseButton.Left,
        Core.MouseButton.Right => SilkMouseButton.Right,
        Core.MouseButton.Middle => SilkMouseButton.Middle,
        _ => SilkMouseButton.Left,
    };

    private static void SetDpiUnaware()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            SetProcessDpiAwarenessContext(-1); // DPI_AWARENESS_CONTEXT_UNAWARE
        }
        catch
        {
            // Pre-Windows 10 1607 or already set — ignore.
        }
    }

    [DllImport("user32", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(nint value);
}
