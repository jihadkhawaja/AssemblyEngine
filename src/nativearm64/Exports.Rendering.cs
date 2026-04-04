using System.Runtime.InteropServices;

namespace AssemblyEngine.NativeArm64;

internal static unsafe partial class NativeExports
{
    [UnmanagedCallersOnly(EntryPoint = "ae_clear")]
    public static void Clear(int r, int g, int b, int a)
    {
        var state = NativeContext.Engine;
        if (state.Framebuffer is null)
            return;

        var color = PackColor(r, g, b, a);
        var pixels = state.Width * state.Height;
        var destination = (uint*)state.Framebuffer;
        for (var index = 0; index < pixels; index++)
            destination[index] = color;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_draw_pixel")]
    public static void DrawPixel(int x, int y, int r, int g, int b, int a)
    {
        WritePixel(x, y, PackColor(r, g, b, a));
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_draw_rect")]
    public static void DrawRect(int x, int y, int w, int h, int r, int g, int b, int a)
    {
        if (w <= 0 || h <= 0)
            return;

        var color = PackColor(r, g, b, a);
        DrawLineImpl(x, y, x + w - 1, y, color);
        DrawLineImpl(x, y + h - 1, x + w - 1, y + h - 1, color);
        DrawLineImpl(x, y, x, y + h - 1, color);
        DrawLineImpl(x + w - 1, y, x + w - 1, y + h - 1, color);
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_draw_filled_rect")]
    public static void DrawFilledRect(int x, int y, int w, int h, int r, int g, int b, int a)
    {
        var state = NativeContext.Engine;
        if (state.Framebuffer is null || w <= 0 || h <= 0)
            return;

        var x1 = Math.Max(x, 0);
        var y1 = Math.Max(y, 0);
        var x2 = Math.Min(x + w, state.Width);
        var y2 = Math.Min(y + h, state.Height);
        if (x1 >= x2 || y1 >= y2)
            return;

        var color = PackColor(r, g, b, a);
        for (var row = y1; row < y2; row++)
        {
            var destination = (uint*)(state.Framebuffer + (row * state.Stride) + (x1 * 4));
            for (var column = x1; column < x2; column++)
                destination[column - x1] = color;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_draw_line")]
    public static void DrawLine(int x1, int y1, int x2, int y2, int r, int g, int b, int a)
    {
        DrawLineImpl(x1, y1, x2, y2, PackColor(r, g, b, a));
    }

    private static void DrawLineImpl(int x1, int y1, int x2, int y2, uint color)
    {
        var dx = Math.Abs(x2 - x1);
        var dy = Math.Abs(y2 - y1);
        var sx = x1 < x2 ? 1 : -1;
        var sy = y1 < y2 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            WritePixel(x1, y1, color);
            if (x1 == x2 && y1 == y2)
                break;

            var twiceError = err * 2;
            if (twiceError > -dy)
            {
                err -= dy;
                x1 += sx;
            }

            if (twiceError < dx)
            {
                err += dx;
                y1 += sy;
            }
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_draw_circle")]
    public static void DrawCircle(int cx, int cy, int radius, int r, int g, int b, int a)
    {
        if (radius <= 0)
            return;

        var color = PackColor(r, g, b, a);
        var x = radius;
        var y = 0;
        var error = 1 - radius;

        while (x >= y)
        {
            PlotCirclePoints(cx, cy, x, y, color);
            y++;
            if (error < 0)
            {
                error += (2 * y) + 1;
            }
            else
            {
                x--;
                error += (2 * (y - x)) + 1;
            }
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_copy_framebuffer")]
    public static unsafe int CopyFramebuffer(byte* destination, int destinationLength)
    {
        var state = NativeContext.Engine;
        if (state.Framebuffer is null || destination is null || destinationLength <= 0)
            return 0;

        var byteCount = checked(state.Width * state.Height * 4);
        if (destinationLength < byteCount)
            return 0;

        Buffer.MemoryCopy(state.Framebuffer, destination, destinationLength, byteCount);
        return byteCount;
    }

    [UnmanagedCallersOnly(EntryPoint = "ae_upload_framebuffer")]
    public static unsafe int UploadFramebuffer(byte* source, int sourceLength)
    {
        var state = NativeContext.Engine;
        if (state.Framebuffer is null || source is null || sourceLength <= 0)
            return 0;

        var byteCount = checked(state.Width * state.Height * 4);
        if (sourceLength < byteCount)
            return 0;

        Buffer.MemoryCopy(source, state.Framebuffer, byteCount, byteCount);
        return 1;
    }

    private static void PlotCirclePoints(int cx, int cy, int x, int y, uint color)
    {
        WritePixel(cx + x, cy + y, color);
        WritePixel(cx + y, cy + x, color);
        WritePixel(cx - y, cy + x, color);
        WritePixel(cx - x, cy + y, color);
        WritePixel(cx - x, cy - y, color);
        WritePixel(cx - y, cy - x, color);
        WritePixel(cx + y, cy - x, color);
        WritePixel(cx + x, cy - y, color);
    }

    private static void WritePixel(int x, int y, uint color)
    {
        var state = NativeContext.Engine;
        if ((uint)x >= (uint)state.Width || (uint)y >= (uint)state.Height || state.Framebuffer is null)
            return;

        var row = state.Framebuffer + (y * state.Stride) + (x * 4);
        *(uint*)row = color;
    }

    private static uint PackColor(int r, int g, int b, int a)
    {
        return (uint)((ClampByte(b)) | (ClampByte(g) << 8) | (ClampByte(r) << 16) | (ClampByte(a) << 24));
    }

    private static int ClampByte(int value)
    {
        if (value < 0)
            return 0;

        return value > 255 ? 255 : value;
    }

    private static uint BlendPixel(uint destination, byte sourceBlue, byte sourceGreen, byte sourceRed, byte sourceAlpha)
    {
        if (sourceAlpha == 0)
            return destination;

        if (sourceAlpha == 255)
            return (uint)(sourceBlue | (sourceGreen << 8) | (sourceRed << 16) | (255u << 24));

        var inverseAlpha = 255 - sourceAlpha;
        var destinationBlue = (byte)(destination & 0xFF);
        var destinationGreen = (byte)((destination >> 8) & 0xFF);
        var destinationRed = (byte)((destination >> 16) & 0xFF);

        var blue = (byte)(((sourceBlue * sourceAlpha) + (destinationBlue * inverseAlpha)) / 255);
        var green = (byte)(((sourceGreen * sourceAlpha) + (destinationGreen * inverseAlpha)) / 255);
        var red = (byte)(((sourceRed * sourceAlpha) + (destinationRed * inverseAlpha)) / 255);

        return (uint)(blue | (green << 8) | (red << 16) | (255u << 24));
    }
}