using AssemblyEngine.Rendering;
using System.Numerics;

namespace AssemblyEngine.Core;

/// <summary>
/// High-level graphics API wrapping the native assembly renderer.
/// </summary>
public static class Graphics
{
    private static readonly UnifiedRenderer Renderer = new();

    public static GraphicsBackend Backend => Renderer.Backend;

    public static Camera3D? ActiveCamera
    {
        get => Renderer.ActiveCamera;
        set => Renderer.ActiveCamera = value;
    }

    internal static bool BeginFrame(int width, int height) => Renderer.BeginFrame(width, height);

    internal static void EndFrame() => Renderer.Present();

    internal static void Shutdown() => Renderer.Shutdown();

    internal static bool TryCopyCurrentFrame(Span<byte> destination, out int bytesWritten) =>
        Renderer.TryCopyFramebuffer(destination, out bytesWritten);

    internal static void SetVSyncEnabled(bool enabled) => Renderer.SetVSyncEnabled(enabled);

    internal static void SetPreferredBackend(GraphicsBackend backend) => Renderer.SetPreferredBackend(backend);

    public static void Clear(Color color) => Renderer.Clear(color);

    public static void DrawPixel(int x, int y, Color color) => Renderer.DrawPixel(x, y, color);

    public static void DrawRect(int x, int y, int w, int h, Color color) => Renderer.DrawRect(x, y, w, h, color);

    public static void DrawFilledRect(int x, int y, int w, int h, Color color) => Renderer.DrawFilledRect(x, y, w, h, color);

    public static void DrawRect(Rectangle rect, Color color) =>
        DrawRect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, color);

    public static void DrawFilledRect(Rectangle rect, Color color) =>
        DrawFilledRect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, color);

    public static void DrawLine(int x1, int y1, int x2, int y2, Color color) => Renderer.DrawLine(x1, y1, x2, y2, color);

    public static void DrawLine(Vector2 from, Vector2 to, Color color) =>
        DrawLine((int)from.X, (int)from.Y, (int)to.X, (int)to.Y, color);

    public static void DrawCircle(int cx, int cy, int radius, Color color) => Renderer.DrawCircle(cx, cy, radius, color);

    public static void DrawCircle(Vector2 center, int radius, Color color) =>
        DrawCircle((int)center.X, (int)center.Y, radius, color);

    public static int LoadSprite(string path) => Renderer.LoadTexture(path);

    public static void DrawSprite(int id, int x, int y, bool alphaBlend = true) => Renderer.DrawSprite(id, x, y, alphaBlend);

    public static void DrawSprite(int id, int x, int y, int width, int height, bool alphaBlend = true) =>
        Renderer.DrawSprite(id, x, y, width, height, alphaBlend);

    public static void DrawSprite(int id, Vector2 position, bool alphaBlend = true) =>
        DrawSprite(id, (int)position.X, (int)position.Y, alphaBlend);

    public static void SetCamera(Camera3D? camera) => ActiveCamera = camera;

    public static void ResetCamera() => ActiveCamera = null;

    public static void DrawMesh(Mesh mesh, Matrix4x4 transform, Color color, bool wireframe = false) =>
        Renderer.DrawMesh(mesh, transform, color, wireframe);

    public static void DrawCube(Matrix4x4 transform, Color color, bool wireframe = false) =>
        Renderer.DrawMesh(Mesh.CreateCube(), transform, color, wireframe);

    internal static void DrawFilledRectDirect(int x, int y, int w, int h, Color color) =>
        Renderer.DrawFilledRectDirect(x, y, w, h, color);
}
