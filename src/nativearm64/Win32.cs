using System.Runtime.InteropServices;

namespace AssemblyEngine.NativeArm64;

internal static partial class Win32
{
    public const uint CS_HREDRAW = 0x0002;
    public const uint CS_VREDRAW = 0x0001;
    public const uint CS_OWNDC = 0x0020;

    public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const uint WS_POPUP = 0x80000000;
    public const uint WS_VISIBLE = 0x10000000;
    public const uint WS_EX_APPWINDOW = 0x00040000;
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    public const int CW_USEDEFAULT = unchecked((int)0x80000000);
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;

    public const int SW_SHOW = 5;
    public const int SW_SHOWMAXIMIZED = 3;
    public const int SW_RESTORE = 9;
    public const uint PM_REMOVE = 0x0001;
    public const int IDC_ARROW = 32512;
    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;

    public const uint WM_DESTROY = 0x0002;
    public const uint WM_SIZE = 0x0005;
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_QUIT = 0x0012;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_MBUTTONDOWN = 0x0207;
    public const uint WM_MBUTTONUP = 0x0208;
    public const uint SIZE_RESTORED = 0;
    public const uint SIZE_MINIMIZED = 1;
    public const uint SIZE_MAXIMIZED = 2;

    public const uint DIB_RGB_COLORS = 0;
    public const uint BI_RGB = 0;
    public const uint SRCCOPY = 0x00CC0020;

    public const uint SND_ASYNC = 0x0001;
    public const uint SND_NODEFAULT = 0x0002;
    public const uint SND_FILENAME = 0x00020000;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public nuint wParam;
        public nint lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public IntPtr lpszMenuName;
        public IntPtr lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RGBQUAD
    {
        public byte rgbBlue;
        public byte rgbGreen;
        public byte rgbRed;
        public byte rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public RGBQUAD bmiColors;
    }

    [LibraryImport("kernel32", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr GetModuleHandle(string? moduleName);

    [LibraryImport("kernel32", EntryPoint = "QueryPerformanceFrequency")]
    internal static partial int QueryPerformanceFrequency(out long frequency);

    [LibraryImport("kernel32", EntryPoint = "QueryPerformanceCounter")]
    internal static partial int QueryPerformanceCounter(out long counter);

    [LibraryImport("user32", EntryPoint = "RegisterClassExW")]
    internal static partial ushort RegisterClassEx(ref WNDCLASSEXW windowClass);

    [LibraryImport("user32", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr CreateWindowEx(
        uint exStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr param);

    [LibraryImport("user32", EntryPoint = "ShowWindow")]
    internal static partial int ShowWindow(IntPtr window, int command);

    [LibraryImport("user32", EntryPoint = "UpdateWindow")]
    internal static partial int UpdateWindow(IntPtr window);

    [LibraryImport("user32", EntryPoint = "DestroyWindow")]
    internal static partial int DestroyWindow(IntPtr window);

    [LibraryImport("user32", EntryPoint = "DefWindowProcW")]
    internal static partial nint DefWindowProc(IntPtr window, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32", EntryPoint = "PeekMessageW")]
    internal static partial int PeekMessage(out MSG message, IntPtr window, uint filterMin, uint filterMax, uint removeMessage);

    [LibraryImport("user32", EntryPoint = "TranslateMessage")]
    internal static partial int TranslateMessage(in MSG message);

    [LibraryImport("user32", EntryPoint = "DispatchMessageW")]
    internal static partial nint DispatchMessage(in MSG message);

    [LibraryImport("user32", EntryPoint = "PostQuitMessage")]
    internal static partial void PostQuitMessage(int exitCode);

    [LibraryImport("user32", EntryPoint = "GetDC")]
    internal static partial IntPtr GetDC(IntPtr window);

    [LibraryImport("user32", EntryPoint = "ReleaseDC")]
    internal static partial int ReleaseDC(IntPtr window, IntPtr deviceContext);

    [LibraryImport("user32", EntryPoint = "LoadCursorW")]
    internal static partial IntPtr LoadCursor(IntPtr instance, nint cursor);

    [LibraryImport("user32", EntryPoint = "AdjustWindowRectEx")]
    internal static partial int AdjustWindowRectEx(ref RECT rect, uint style, int hasMenu, uint exStyle);

    [LibraryImport("user32", EntryPoint = "SetWindowPos")]
    internal static partial int SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32", EntryPoint = "GetWindowRect")]
    internal static partial int GetWindowRect(IntPtr window, out RECT rect);

    [LibraryImport("user32", EntryPoint = "GetWindowLongPtrW")]
    internal static partial IntPtr GetWindowLongPtr(IntPtr window, int index);

    [LibraryImport("user32", EntryPoint = "SetWindowLongPtrW")]
    internal static partial IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr newLong);

    [LibraryImport("user32", EntryPoint = "GetSystemMetrics")]
    internal static partial int GetSystemMetrics(int index);

    [LibraryImport("user32", EntryPoint = "InvalidateRect")]
    internal static partial int InvalidateRect(IntPtr window, IntPtr rect, int erase);

    [LibraryImport("gdi32", EntryPoint = "StretchDIBits")]
    internal static unsafe partial int StretchDIBits(
        IntPtr deviceContext,
        int xDest,
        int yDest,
        int destWidth,
        int destHeight,
        int xSrc,
        int ySrc,
        int srcWidth,
        int srcHeight,
        void* bits,
        ref BITMAPINFO bitsInfo,
        uint usage,
        uint rasterOperation);

    [LibraryImport("winmm", EntryPoint = "PlaySoundW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int PlaySound(string? soundName, IntPtr module, uint flags);

    [LibraryImport("dwmapi", EntryPoint = "DwmFlush")]
    internal static partial int DwmFlush();
}