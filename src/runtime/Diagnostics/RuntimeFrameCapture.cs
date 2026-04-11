using AssemblyEngine.Core;
using AssemblyEngine.Platform;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EngineGraphics = AssemblyEngine.Core.Graphics;
using DrawingRectangle = System.Drawing.Rectangle;

namespace AssemblyEngine.Diagnostics;

[SupportedOSPlatform("windows")]
internal static class RuntimeFrameCapture
{
    public static RuntimeScreenshot CapturePng()
    {
        int width = EngineHost.WindowWidth;
        int height = EngineHost.WindowHeight;
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("The engine window is not initialized yet.");

        byte[] framebuffer = new byte[checked(width * height * 4)];
        int copiedBytes;

        unsafe
        {
            copiedBytes = EngineGraphics.TryCopyCurrentFrame(framebuffer, out var managedBytes)
                ? managedBytes
                : 0;
        }

        if (copiedBytes != framebuffer.Length)
            throw new InvalidOperationException("Failed to capture the current framebuffer.");

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var bounds = new DrawingRectangle(0, 0, width, height);
        BitmapData? bitmapData = null;

        try
        {
            bitmapData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(framebuffer, 0, bitmapData.Scan0, framebuffer.Length);
        }
        finally
        {
            if (bitmapData is not null)
                bitmap.UnlockBits(bitmapData);
        }

        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        return new RuntimeScreenshot(output.ToArray(), "image/png", width, height);
    }
}