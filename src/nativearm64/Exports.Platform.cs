using System.Runtime.InteropServices;
using AssemblyEngine.NativeArm64.Platform.Windows;

namespace AssemblyEngine.NativeArm64;

internal static unsafe partial class NativeExports
{
    [UnmanagedCallersOnly(EntryPoint = "ae_init")]
    public static int Init(int width, int height, byte* title)
    {
        if (width <= 0 || height <= 0)
            return 0;

        var state = NativeContext.Engine;
        if (state.Running)
            return 1;

        var titleText = Utf8ToString(title) ?? NativeContext.DefaultTitle;
        if (!WindowsDisplay.TryInitialize(state, width, height, titleText))
            return 0;

        Win32.QueryPerformanceFrequency(out state.PerfFrequency);
        Win32.QueryPerformanceCounter(out state.LastTick);
        state.CurrentTick = state.LastTick;
        state.DeltaTime = 0f;
        state.Fps = 0;
        state.FrameCount = 0;
        state.FpsAccumulator = 0f;
        state.VSyncEnabled = true;
        state.WindowMode = WindowMode.Windowed;
        state.HasRestoreWindowRect = false;
        state.ClearAssets();
        state.EnsureArena();
        state.Running = true;
        return 1;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_shutdown")]
    public static void Shutdown()
    {
        var state = NativeContext.Engine;
        state.Running = false;
        StopSoundImpl();
        WindowsDisplay.Shutdown(state);
        state.ReleaseArena();
        state.ClearAssets();
        Array.Clear(state.Keys);
        Array.Clear(state.PrevKeys);
        state.MouseButtons = 0;
        state.PrevMouseButtons = 0;
        state.MouseX = 0;
        state.MouseY = 0;
        state.WindowMode = WindowMode.Windowed;
        state.HasRestoreWindowRect = false;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_poll_events")]
    public static int PollEvents()
    {
        var state = NativeContext.Engine;
        state.ResetFrameInput();
        UpdateTimer(state);

        while (Win32.PeekMessage(out var message, IntPtr.Zero, 0, 0, Win32.PM_REMOVE) != 0)
        {
            if (message.message == Win32.WM_QUIT)
            {
                state.Running = false;
                break;
            }

            Win32.TranslateMessage(in message);
            Win32.DispatchMessage(in message);
        }

        return state.Running ? 1 : 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_present")]
    public static void Present()
    {
        WindowsDisplay.Present(NativeContext.Engine);
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_set_vsync_enabled")]
    public static void SetVSyncEnabled(int enabled)
    {
        NativeContext.Engine.VSyncEnabled = enabled != 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_resize_window")]
    public static int ResizeWindow(int width, int height)
    {
        return WindowsDisplay.Resize(NativeContext.Engine, width, height) ? 1 : 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_set_window_mode")]
    public static int SetWindowMode(int windowMode)
    {
        if (!Enum.IsDefined((WindowMode)windowMode))
            return 0;

        return WindowsDisplay.SetWindowMode(NativeContext.Engine, (WindowMode)windowMode) ? 1 : 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_get_window_mode")]
    public static int GetWindowMode()
    {
        return (int)NativeContext.Engine.WindowMode;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_get_window_width")]
    public static int GetWindowWidth()
    {
        return NativeContext.Engine.Width;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_get_window_height")]
    public static int GetWindowHeight()
    {
        return NativeContext.Engine.Height;
    }

    private static void UpdateTimer(EngineState state)
    {
        if (state.PerfFrequency <= 0)
            return;

        Win32.QueryPerformanceCounter(out state.CurrentTick);
        var elapsedTicks = state.CurrentTick - state.LastTick;
        state.LastTick = state.CurrentTick;

        state.DeltaTime = elapsedTicks <= 0
            ? 0f
            : (float)elapsedTicks / state.PerfFrequency;

        state.FrameCount++;
        state.FpsAccumulator += state.DeltaTime;
        if (state.FpsAccumulator >= 1f)
        {
            state.Fps = state.FrameCount;
            state.FrameCount = 0;
            state.FpsAccumulator = 0f;
        }
    }

    private static string? Utf8ToString(byte* value)
    {
        return value is null ? null : Marshal.PtrToStringUTF8((IntPtr)value);
    }
}