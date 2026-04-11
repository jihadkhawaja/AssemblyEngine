using AssemblyEngine.Core;
using System.Numerics;

namespace AssemblyEngine.Rendering;

internal static class SoftwareRasterizer3D
{
    public static void DrawMesh(RenderSurface surface, Mesh mesh, Matrix4x4 transform, Camera3D camera, Color color, bool wireframe)
    {
        if (mesh.Vertices.Count == 0 || mesh.Indices.Count < 3)
            return;

        surface.EnsureDepthCleared();

        var aspectRatio = surface.Height == 0 ? 1f : (float)surface.Width / surface.Height;
        var worldViewProjection = transform * camera.CreateViewMatrix() * camera.CreateProjectionMatrix(aspectRatio);

        for (var index = 0; index < mesh.Indices.Count; index += 3)
        {
            var v0 = Project(surface, mesh.Vertices[mesh.Indices[index]].Position, worldViewProjection);
            var v1 = Project(surface, mesh.Vertices[mesh.Indices[index + 1]].Position, worldViewProjection);
            var v2 = Project(surface, mesh.Vertices[mesh.Indices[index + 2]].Position, worldViewProjection);
            if (!v0.Visible || !v1.Visible || !v2.Visible)
                continue;

            if (wireframe)
            {
                SoftwareRasterizer2D.DrawLine(surface, (int)v0.X, (int)v0.Y, (int)v1.X, (int)v1.Y, color);
                SoftwareRasterizer2D.DrawLine(surface, (int)v1.X, (int)v1.Y, (int)v2.X, (int)v2.Y, color);
                SoftwareRasterizer2D.DrawLine(surface, (int)v2.X, (int)v2.Y, (int)v0.X, (int)v0.Y, color);
                continue;
            }

            RasterizeTriangle(surface, v0, v1, v2, color);
        }
    }

    private static ProjectedVertex Project(RenderSurface surface, Vector3 position, Matrix4x4 worldViewProjection)
    {
        var clip = Vector4.Transform(new Vector4(position, 1f), worldViewProjection);
        if (clip.W <= 0.0001f)
            return default;

        var inverseW = 1f / clip.W;
        var ndc = new Vector3(clip.X * inverseW, clip.Y * inverseW, clip.Z * inverseW);
        if (ndc.Z < -1f || ndc.Z > 1f)
            return default;

        var screenX = (ndc.X + 1f) * 0.5f * (surface.Width - 1);
        var screenY = (1f - ((ndc.Y + 1f) * 0.5f)) * (surface.Height - 1);
        return new ProjectedVertex(screenX, screenY, (ndc.Z + 1f) * 0.5f, true);
    }

    private static void RasterizeTriangle(RenderSurface surface, ProjectedVertex a, ProjectedVertex b, ProjectedVertex c, Color color)
    {
        var minX = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        var maxX = Math.Min(surface.Width - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        var minY = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
        var maxY = Math.Min(surface.Height - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));

        var area = Edge(a.X, a.Y, b.X, b.Y, c.X, c.Y);
        if (MathF.Abs(area) <= float.Epsilon)
            return;

        if (area < 0f)
        {
            (b, c) = (c, b);
            area = -area;
        }

        var packed = RenderSurface.PackColor(color);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var px = x + 0.5f;
                var py = y + 0.5f;

                var w0 = Edge(b.X, b.Y, c.X, c.Y, px, py);
                var w1 = Edge(c.X, c.Y, a.X, a.Y, px, py);
                var w2 = Edge(a.X, a.Y, b.X, b.Y, px, py);
                if (w0 < 0f || w1 < 0f || w2 < 0f)
                    continue;

                w0 /= area;
                w1 /= area;
                w2 /= area;

                var depth = (a.Depth * w0) + (b.Depth * w1) + (c.Depth * w2);
                var index = (y * surface.Width) + x;
                if (depth >= surface.DepthBuffer[index])
                    continue;

                surface.DepthBuffer[index] = depth;
                surface.ColorBuffer[index] = color.A == 255
                    ? packed
                    : Blend(surface.ColorBuffer[index], color);
            }
        }
    }

    private static uint Blend(uint destination, Color color)
    {
        var inverseAlpha = 255 - color.A;
        var destinationBlue = (byte)(destination & 0xFF);
        var destinationGreen = (byte)((destination >> 8) & 0xFF);
        var destinationRed = (byte)((destination >> 16) & 0xFF);

        var blue = (byte)(((color.B * color.A) + (destinationBlue * inverseAlpha)) / 255);
        var green = (byte)(((color.G * color.A) + (destinationGreen * inverseAlpha)) / 255);
        var red = (byte)(((color.R * color.A) + (destinationRed * inverseAlpha)) / 255);

        return (uint)(blue | (green << 8) | (red << 16) | (255u << 24));
    }

    private static float Edge(float ax, float ay, float bx, float by, float px, float py)
    {
        return ((px - ax) * (by - ay)) - ((py - ay) * (bx - ax));
    }

    private readonly record struct ProjectedVertex(float X, float Y, float Depth, bool Visible);
}