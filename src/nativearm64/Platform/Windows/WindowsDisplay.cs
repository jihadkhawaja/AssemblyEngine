using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AssemblyEngine.NativeArm64.Platform.Windows;

internal static unsafe class WindowsDisplay
{
    public static bool TryInitialize(EngineState state, int width, int height, string titleText)
    {
        state.EnsureClassName();
        state.Instance = Win32.GetModuleHandle(null);

        if (!TryAllocateFramebuffer(width, height, out var framebuffer, out var stride, out var bitmapInfo))
            return false;

        var rect = CreateAdjustedWindowRect(width, height);
        var windowClass = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)Unsafe.SizeOf<Win32.WNDCLASSEXW>(),
            style = Win32.CS_HREDRAW | Win32.CS_VREDRAW | Win32.CS_OWNDC,
            lpfnWndProc = (IntPtr)(delegate* unmanaged<IntPtr, uint, nuint, nint, nint>)&WindowProc,
            hInstance = state.Instance,
            hCursor = Win32.LoadCursor(IntPtr.Zero, Win32.IDC_ARROW),
            lpszClassName = state.ClassNamePtr
        };

        Win32.RegisterClassEx(ref windowClass);

        state.Window = Win32.CreateWindowEx(
            Win32.WS_EX_APPWINDOW,
            NativeContext.WindowClassName,
            titleText,
            Win32.WS_OVERLAPPEDWINDOW,
            Win32.CW_USEDEFAULT,
            Win32.CW_USEDEFAULT,
            rect.Right - rect.Left,
            rect.Bottom - rect.Top,
            IntPtr.Zero,
            IntPtr.Zero,
            state.Instance,
            IntPtr.Zero);

        if (state.Window == IntPtr.Zero)
        {
            NativeMemory.Free(framebuffer);
            return false;
        }

        state.DeviceContext = Win32.GetDC(state.Window);
        if (state.DeviceContext == IntPtr.Zero)
        {
            Win32.DestroyWindow(state.Window);
            state.Window = IntPtr.Zero;
            NativeMemory.Free(framebuffer);
            return false;
        }

        state.Framebuffer = framebuffer;
        state.Width = width;
        state.Height = height;
        state.Stride = stride;
        state.BitmapInfo = bitmapInfo;
        state.WindowMode = WindowMode.Windowed;
        state.HasRestoreWindowRect = false;

        Win32.ShowWindow(state.Window, Win32.SW_SHOW);
        Win32.UpdateWindow(state.Window);
        return true;
    }

    public static void Shutdown(EngineState state)
    {
        if (state.DeviceContext != IntPtr.Zero && state.Window != IntPtr.Zero)
        {
            Win32.ReleaseDC(state.Window, state.DeviceContext);
            state.DeviceContext = IntPtr.Zero;
        }

        if (state.Window != IntPtr.Zero)
        {
            Win32.DestroyWindow(state.Window);
            state.Window = IntPtr.Zero;
        }

        ReleaseFramebuffer(state);
    }

    public static void Present(EngineState state)
    {
        if (state.DeviceContext == IntPtr.Zero || state.Framebuffer is null)
            return;

        Win32.StretchDIBits(
            state.DeviceContext,
            0,
            0,
            state.Width,
            state.Height,
            0,
            0,
            state.Width,
            state.Height,
            state.Framebuffer,
            ref state.BitmapInfo,
            Win32.DIB_RGB_COLORS,
            Win32.SRCCOPY);

        if (state.VSyncEnabled)
            Win32.DwmFlush();
    }

    public static bool Resize(EngineState state, int width, int height)
    {
        if (width <= 0 || height <= 0 || state.Window == IntPtr.Zero)
            return false;

        if (state.WindowMode != WindowMode.Windowed && !SetWindowMode(state, WindowMode.Windowed))
            return false;

        var rect = CreateAdjustedWindowRect(width, height);
        if (Win32.SetWindowPos(
                state.Window,
                IntPtr.Zero,
                0,
                0,
                rect.Right - rect.Left,
                rect.Bottom - rect.Top,
                Win32.SWP_NOMOVE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE) == 0)
        {
            return false;
        }

        return true;
    }

    public static bool SetWindowMode(EngineState state, WindowMode windowMode)
    {
        if (!Enum.IsDefined(windowMode) || state.Window == IntPtr.Zero)
            return false;

        if (windowMode == state.WindowMode)
            return true;

        switch (windowMode)
        {
            case WindowMode.Windowed:
                return RestoreWindowedMode(state);

            case WindowMode.MaximizedWindow:
                if (state.WindowMode == WindowMode.BorderlessFullscreen && !RestoreWindowedStyleAndBounds(state))
                    return false;

                Win32.ShowWindow(state.Window, Win32.SW_SHOWMAXIMIZED);
                state.WindowMode = WindowMode.MaximizedWindow;
                return true;

            case WindowMode.BorderlessFullscreen:
                if (state.WindowMode == WindowMode.MaximizedWindow)
                    Win32.ShowWindow(state.Window, Win32.SW_RESTORE);

                CaptureRestoreWindowRect(state);
                ApplyBorderlessStyle(state);
                state.WindowMode = WindowMode.BorderlessFullscreen;

                return Win32.SetWindowPos(
                        state.Window,
                        IntPtr.Zero,
                        0,
                        0,
                        Win32.GetSystemMetrics(Win32.SM_CXSCREEN),
                        Win32.GetSystemMetrics(Win32.SM_CYSCREEN),
                        Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED) != 0;

            default:
                return false;
        }
    }

    [UnmanagedCallersOnly]
    private static nint WindowProc(IntPtr window, uint message, nuint wParam, nint lParam)
    {
        var state = NativeContext.Engine;

        switch (message)
        {
            case Win32.WM_CLOSE:
                state.Running = false;
                Win32.DestroyWindow(window);
                return 0;
            case Win32.WM_DESTROY:
                state.Running = false;
                Win32.PostQuitMessage(0);
                return 0;
            case Win32.WM_SIZE:
                var width = (ushort)((nuint)lParam & 0xFFFF);
                var height = (ushort)(((nuint)lParam >> 16) & 0xFFFF);
                if ((uint)wParam != Win32.SIZE_MINIMIZED && width > 0 && height > 0)
                {
                    if (state.WindowMode != WindowMode.BorderlessFullscreen)
                    {
                        state.WindowMode = (uint)wParam == Win32.SIZE_MAXIMIZED
                            ? WindowMode.MaximizedWindow
                            : WindowMode.Windowed;
                    }

                    ApplyClientSize(state, width, height);
                }

                return 0;
            case Win32.WM_KEYDOWN:
                state.Keys[(int)(wParam & 0xFF)] = 1;
                return 0;
            case Win32.WM_KEYUP:
                state.Keys[(int)(wParam & 0xFF)] = 0;
                return 0;
            case Win32.WM_MOUSEMOVE:
                state.MouseX = unchecked((short)(lParam & 0xFFFF));
                state.MouseY = unchecked((short)((lParam >> 16) & 0xFFFF));
                return 0;
            case Win32.WM_LBUTTONDOWN:
                state.MouseButtons |= 1;
                return 0;
            case Win32.WM_LBUTTONUP:
                state.MouseButtons &= ~1;
                return 0;
            case Win32.WM_RBUTTONDOWN:
                state.MouseButtons |= 2;
                return 0;
            case Win32.WM_RBUTTONUP:
                state.MouseButtons &= ~2;
                return 0;
            case Win32.WM_MBUTTONDOWN:
                state.MouseButtons |= 4;
                return 0;
            case Win32.WM_MBUTTONUP:
                state.MouseButtons &= ~4;
                return 0;
            default:
                return Win32.DefWindowProc(window, message, wParam, lParam);
        }
    }

    private static Win32.RECT CreateAdjustedWindowRect(int width, int height)
    {
        var rect = new Win32.RECT
        {
            Left = 0,
            Top = 0,
            Right = width,
            Bottom = height
        };

        Win32.AdjustWindowRectEx(ref rect, Win32.WS_OVERLAPPEDWINDOW, 0, Win32.WS_EX_APPWINDOW);
        return rect;
    }

    private static bool ApplyClientSize(EngineState state, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return false;

        if (state.Width == width && state.Height == height && state.Framebuffer is not null)
            return true;

        if (!TryAllocateFramebuffer(width, height, out var newFramebuffer, out var newStride, out var bitmapInfo))
            return false;

        var oldFramebuffer = state.Framebuffer;
        state.Framebuffer = newFramebuffer;
        state.Width = width;
        state.Height = height;
        state.Stride = newStride;
        state.BitmapInfo = bitmapInfo;

        if (oldFramebuffer is not null)
            NativeMemory.Free(oldFramebuffer);

        if (state.Window != IntPtr.Zero)
            Win32.InvalidateRect(state.Window, IntPtr.Zero, 0);

        return true;
    }

    private static void CaptureRestoreWindowRect(EngineState state)
    {
        if (state.Window == IntPtr.Zero)
            return;

        if (Win32.GetWindowRect(state.Window, out var rect) == 0)
            return;

        state.RestoreWindowRect = rect;
        state.HasRestoreWindowRect = true;
    }

    private static bool RestoreWindowedMode(EngineState state)
    {
        if (state.WindowMode == WindowMode.BorderlessFullscreen)
            return RestoreWindowedStyleAndBounds(state);

        Win32.ShowWindow(state.Window, Win32.SW_RESTORE);
        state.WindowMode = WindowMode.Windowed;
        return true;
    }

    private static bool RestoreWindowedStyleAndBounds(EngineState state)
    {
        ApplyWindowedStyle(state);

        if (!state.HasRestoreWindowRect)
        {
            state.WindowMode = WindowMode.Windowed;
            Win32.ShowWindow(state.Window, Win32.SW_RESTORE);
            return true;
        }

        var restoreRect = state.RestoreWindowRect;
        state.WindowMode = WindowMode.Windowed;

        if (Win32.SetWindowPos(
                state.Window,
                IntPtr.Zero,
                restoreRect.Left,
                restoreRect.Top,
                restoreRect.Right - restoreRect.Left,
                restoreRect.Bottom - restoreRect.Top,
                Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED) == 0)
        {
            return false;
        }

        Win32.ShowWindow(state.Window, Win32.SW_RESTORE);
        return true;
    }

    private static void ApplyWindowedStyle(EngineState state)
    {
        Win32.SetWindowLongPtr(state.Window, Win32.GWL_STYLE, (IntPtr)unchecked((nint)(Win32.WS_OVERLAPPEDWINDOW | Win32.WS_VISIBLE)));
        Win32.SetWindowLongPtr(state.Window, Win32.GWL_EXSTYLE, (IntPtr)unchecked((nint)Win32.WS_EX_APPWINDOW));
    }

    private static void ApplyBorderlessStyle(EngineState state)
    {
        Win32.SetWindowLongPtr(state.Window, Win32.GWL_STYLE, (IntPtr)unchecked((nint)(Win32.WS_POPUP | Win32.WS_VISIBLE)));
        Win32.SetWindowLongPtr(state.Window, Win32.GWL_EXSTYLE, (IntPtr)unchecked((nint)Win32.WS_EX_APPWINDOW));
    }

    private static bool TryAllocateFramebuffer(int width, int height, out byte* framebuffer, out int stride, out Win32.BITMAPINFO bitmapInfo)
    {
        stride = width * 4;
        framebuffer = (byte*)NativeMemory.AllocZeroed((nuint)(stride * height));
        bitmapInfo = CreateBitmapInfo(width, height);
        return framebuffer is not null;
    }

    private static Win32.BITMAPINFO CreateBitmapInfo(int width, int height)
    {
        return new Win32.BITMAPINFO
        {
            bmiHeader = new Win32.BITMAPINFOHEADER
            {
                biSize = (uint)Unsafe.SizeOf<Win32.BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = Win32.BI_RGB,
                biSizeImage = (uint)(width * height * 4)
            }
        };
    }

    private static void ReleaseFramebuffer(EngineState state)
    {
        if (state.Framebuffer is null)
            return;

        NativeMemory.Free(state.Framebuffer);
        state.Framebuffer = null;
    }
}