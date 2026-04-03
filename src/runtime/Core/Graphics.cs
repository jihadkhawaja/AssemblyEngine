using AssemblyEngine.Interop;

namespace AssemblyEngine.Core;

/// <summary>
/// High-level graphics API wrapping the native assembly renderer.
/// </summary>
public static class Graphics
{
    public static void Clear(Color color) =>
        NativeCore.Clear(color.R, color.G, color.B, color.A);

    public static void DrawPixel(int x, int y, Color color) =>
        NativeCore.DrawPixel(x, y, color.R, color.G, color.B, color.A);

    public static void DrawRect(int x, int y, int w, int h, Color color) =>
        NativeCore.DrawRect(x, y, w, h, color.R, color.G, color.B, color.A);

    public static void DrawFilledRect(int x, int y, int w, int h, Color color) =>
        NativeCore.DrawFilledRect(x, y, w, h, color.R, color.G, color.B, color.A);

    public static void DrawRect(Rectangle rect, Color color) =>
        DrawRect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, color);

    public static void DrawFilledRect(Rectangle rect, Color color) =>
        DrawFilledRect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, color);

    public static void DrawLine(int x1, int y1, int x2, int y2, Color color) =>
        NativeCore.DrawLine(x1, y1, x2, y2, color.R, color.G, color.B, color.A);

    public static void DrawLine(Vector2 from, Vector2 to, Color color) =>
        DrawLine((int)from.X, (int)from.Y, (int)to.X, (int)to.Y, color);

    public static void DrawCircle(int cx, int cy, int radius, Color color) =>
        NativeCore.DrawCircle(cx, cy, radius, color.R, color.G, color.B, color.A);

    public static void DrawCircle(Vector2 center, int radius, Color color) =>
        DrawCircle((int)center.X, (int)center.Y, radius, color);

    public static int LoadSprite(string path) => NativeCore.LoadSprite(path);

    public static void DrawSprite(int id, int x, int y, bool alphaBlend = true) =>
        NativeCore.DrawSprite(id, x, y, alphaBlend ? 1 : 0);

    public static void DrawSprite(int id, Vector2 position, bool alphaBlend = true) =>
        DrawSprite(id, (int)position.X, (int)position.Y, alphaBlend);
}
