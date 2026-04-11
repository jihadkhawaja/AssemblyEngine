using AssemblyEngine.Core;
using System.Runtime.Versioning;

namespace AssemblyEngine.Rendering;

[SupportedOSPlatform("windows")]
internal static class SoftwareRasterizer2D
{
    public static void DrawPixel(RenderSurface surface, int x, int y, Color color)
    {
        if ((uint)x >= (uint)surface.Width || (uint)y >= (uint)surface.Height)
            return;

        Blend(surface.ColorBuffer, y * surface.Width + x, color);
    }

    public static void DrawFilledRect(RenderSurface surface, int x, int y, int width, int height, Color color)
    {
        if (width <= 0 || height <= 0)
            return;

        var x1 = Math.Max(0, x);
        var y1 = Math.Max(0, y);
        var x2 = Math.Min(surface.Width, x + width);
        var y2 = Math.Min(surface.Height, y + height);
        if (x1 >= x2 || y1 >= y2)
            return;

        var fillWidth = x2 - x1;
        if (color.A == 255)
        {
            var packed = RenderSurface.PackColor(color);
            var buffer = surface.ColorBuffer.AsSpan();
            for (var row = y1; row < y2; row++)
                buffer.Slice(row * surface.Width + x1, fillWidth).Fill(packed);
        }
        else
        {
            for (var row = y1; row < y2; row++)
            {
                var rowOffset = row * surface.Width;
                for (var column = x1; column < x2; column++)
                    Blend(surface.ColorBuffer, rowOffset + column, color);
            }
        }
    }

    public static void DrawRect(RenderSurface surface, int x, int y, int width, int height, Color color)
    {
        if (width <= 0 || height <= 0)
            return;

        DrawLine(surface, x, y, x + width - 1, y, color);
        DrawLine(surface, x, y + height - 1, x + width - 1, y + height - 1, color);
        DrawLine(surface, x, y, x, y + height - 1, color);
        DrawLine(surface, x + width - 1, y, x + width - 1, y + height - 1, color);
    }

    public static void DrawLine(RenderSurface surface, int x1, int y1, int x2, int y2, Color color)
    {
        var dx = Math.Abs(x2 - x1);
        var dy = Math.Abs(y2 - y1);
        var sx = x1 < x2 ? 1 : -1;
        var sy = y1 < y2 ? 1 : -1;
        var error = dx - dy;

        while (true)
        {
            DrawPixel(surface, x1, y1, color);
            if (x1 == x2 && y1 == y2)
                break;

            var twiceError = error * 2;
            if (twiceError > -dy)
            {
                error -= dy;
                x1 += sx;
            }

            if (twiceError < dx)
            {
                error += dx;
                y1 += sy;
            }
        }
    }

    public static void DrawCircle(RenderSurface surface, int cx, int cy, int radius, Color color)
    {
        if (radius <= 0)
            return;

        var x = radius;
        var y = 0;
        var error = 1 - radius;

        while (x >= y)
        {
            PlotCircle(surface, cx, cy, x, y, color);
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

    public static void DrawSprite(RenderSurface surface, Texture2D texture, int x, int y, bool alphaBlend)
    {
        for (var row = 0; row < texture.Height; row++)
        {
            var destY = y + row;
            if ((uint)destY >= (uint)surface.Height)
                continue;

            for (var column = 0; column < texture.Width; column++)
            {
                var destX = x + column;
                if ((uint)destX >= (uint)surface.Width)
                    continue;

                var sourceIndex = (row * texture.Pitch) + (column * 4);
                var sourceBlue = texture.Pixels[sourceIndex];
                var sourceGreen = texture.Pixels[sourceIndex + 1];
                var sourceRed = texture.Pixels[sourceIndex + 2];
                var sourceAlpha = texture.Pixels[sourceIndex + 3];
                if (sourceAlpha == 0)
                    continue;

                var destinationIndex = (destY * surface.Width) + destX;
                surface.ColorBuffer[destinationIndex] = alphaBlend
                    ? Blend(surface.ColorBuffer[destinationIndex], sourceBlue, sourceGreen, sourceRed, sourceAlpha)
                    : (uint)(sourceBlue | (sourceGreen << 8) | (sourceRed << 16) | (sourceAlpha << 24));
            }
        }
    }

    private static void PlotCircle(RenderSurface surface, int cx, int cy, int x, int y, Color color)
    {
        DrawPixel(surface, cx + x, cy + y, color);
        DrawPixel(surface, cx + y, cy + x, color);
        DrawPixel(surface, cx - y, cy + x, color);
        DrawPixel(surface, cx - x, cy + y, color);
        DrawPixel(surface, cx - x, cy - y, color);
        DrawPixel(surface, cx - y, cy - x, color);
        DrawPixel(surface, cx + y, cy - x, color);
        DrawPixel(surface, cx + x, cy - y, color);
    }

    private static void Blend(uint[] buffer, int index, Color color)
    {
        buffer[index] = color.A == 255
            ? RenderSurface.PackColor(color)
            : Blend(buffer[index], color.B, color.G, color.R, color.A);
    }

    private static uint Blend(uint destination, byte sourceBlue, byte sourceGreen, byte sourceRed, byte sourceAlpha)
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