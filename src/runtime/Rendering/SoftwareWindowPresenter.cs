using AssemblyEngine.Interop;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AssemblyEngine.Rendering;

[SupportedOSPlatform("windows")]
internal sealed unsafe class SoftwareWindowPresenter : IDisposable
{
    private const uint DIB_RGB_COLORS = 0;
    private const uint SRCCOPY = 0x00CC0020;
    private const uint BI_RGB = 0;

    private nint _windowHandle;
    private nint _deviceContext;
    private bool _vSyncEnabled = true;
    private BitmapInfo _bitmapInfo;

    public void SetVSyncEnabled(bool enabled)
    {
        _vSyncEnabled = enabled;
    }

    public bool Present(IntPtr framebuffer, int width, int height, int stride)
    {
        if (framebuffer == IntPtr.Zero || width <= 0 || height <= 0 || stride <= 0)
            return false;

        EnsureDeviceContext();
        if (_deviceContext == IntPtr.Zero)
            return false;

        _bitmapInfo = CreateBitmapInfo(width, height, stride);
        Win32.StretchDIBits(
            _deviceContext,
            0,
            0,
            width,
            height,
            0,
            0,
            width,
            height,
            framebuffer,
            ref _bitmapInfo,
            DIB_RGB_COLORS,
            SRCCOPY);

        if (_vSyncEnabled)
            Win32.DwmFlush();

        return true;
    }

    public void Dispose()
    {
        if (_deviceContext != IntPtr.Zero && _windowHandle != IntPtr.Zero)
        {
            Win32.ReleaseDC(_windowHandle, _deviceContext);
            _deviceContext = IntPtr.Zero;
        }

        _windowHandle = IntPtr.Zero;
    }

    private void EnsureDeviceContext()
    {
        if (_deviceContext != IntPtr.Zero)
            return;

        _windowHandle = NativeCore.GetWindowHandle();
        if (_windowHandle == IntPtr.Zero)
            return;

        _deviceContext = Win32.GetDC(_windowHandle);
    }

    private static BitmapInfo CreateBitmapInfo(int width, int height, int stride)
    {
        return new BitmapInfo
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
                Compression = BI_RGB,
                SizeImage = (uint)(stride * height)
            }
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RgbQuad
    {
        public byte Blue;
        public byte Green;
        public byte Red;
        public byte Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public RgbQuad Colors;
    }

    private static partial class Win32
    {
        [DllImport("user32", EntryPoint = "GetDC")]
        internal static extern nint GetDC(nint window);

        [DllImport("user32", EntryPoint = "ReleaseDC")]
        internal static extern int ReleaseDC(nint window, nint deviceContext);

        [DllImport("gdi32", EntryPoint = "StretchDIBits")]
        internal static extern int StretchDIBits(
            nint deviceContext,
            int xDest,
            int yDest,
            int destWidth,
            int destHeight,
            int xSrc,
            int ySrc,
            int srcWidth,
            int srcHeight,
            IntPtr bits,
            ref BitmapInfo bitsInfo,
            uint usage,
            uint rasterOperation);

        [DllImport("dwmapi", EntryPoint = "DwmFlush")]
        internal static extern int DwmFlush();
    }
}