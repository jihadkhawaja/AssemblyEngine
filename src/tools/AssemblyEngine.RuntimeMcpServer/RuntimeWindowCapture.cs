using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AssemblyEngine.Diagnostics;

namespace AssemblyEngine.RuntimeMcpServer;

[SupportedOSPlatform("windows")]
internal static class RuntimeWindowCapture
{
    public static RuntimeScreenshot CaptureClientArea(Process process)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Window capture is only available on Windows.");

        if (process.HasExited)
            throw new InvalidOperationException("The game process has already exited.");

        process.Refresh();
        nint windowHandle = process.MainWindowHandle;
        if (windowHandle == IntPtr.Zero)
            throw new InvalidOperationException("The game window is not available yet.");

        if (!GetClientRect(windowHandle, out RECT clientRect))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to read the game window client area.");

        int width = clientRect.Right - clientRect.Left;
        int height = clientRect.Bottom - clientRect.Top;
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("The game window client area is not initialized yet.");

        nint windowDc = GetDC(windowHandle);
        if (windowDc == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to get the game window device context.");

        nint memoryDc = IntPtr.Zero;
        nint bitmapHandle = IntPtr.Zero;
        nint previousBitmap = IntPtr.Zero;

        try
        {
            memoryDc = CreateCompatibleDC(windowDc);
            if (memoryDc == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create a compatible memory device context.");

            bitmapHandle = CreateCompatibleBitmap(windowDc, width, height);
            if (bitmapHandle == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create a compatible bitmap for the game window capture.");

            previousBitmap = SelectObject(memoryDc, bitmapHandle);
            if (previousBitmap == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to select the capture bitmap into the memory device context.");

            if (!BitBlt(memoryDc, 0, 0, width, height, windowDc, 0, 0, SRCCOPY))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to capture the game window client area.");

            using var bitmap = Image.FromHbitmap(bitmapHandle);
            using var output = new MemoryStream();
            bitmap.Save(output, ImageFormat.Png);
            return new RuntimeScreenshot(output.ToArray(), "image/png", width, height);
        }
        finally
        {
            if (previousBitmap != IntPtr.Zero && memoryDc != IntPtr.Zero)
                SelectObject(memoryDc, previousBitmap);

            if (bitmapHandle != IntPtr.Zero)
                DeleteObject(bitmapHandle);

            if (memoryDc != IntPtr.Zero)
                DeleteDC(memoryDc);

            ReleaseDC(windowHandle, windowDc);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(nint hWnd, nint hDc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateCompatibleDC(nint hDc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateCompatibleBitmap(nint hDc, int nWidth, int nHeight);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint SelectObject(nint hDc, nint hGdiObj);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint hDc);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(
        nint hdcDest,
        int x,
        int y,
        int cx,
        int cy,
        nint hdcSrc,
        int x1,
        int y1,
        int rop);

    private const int SRCCOPY = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}