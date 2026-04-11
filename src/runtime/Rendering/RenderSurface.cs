using AssemblyEngine.Core;

namespace AssemblyEngine.Rendering;

internal sealed class RenderSurface
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int Stride => Width * 4;
    public uint[] ColorBuffer { get; private set; } = [];
    public float[] DepthBuffer { get; private set; } = [];
    public int ByteLength => ColorBuffer.Length * sizeof(uint);

    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(width <= 0 ? nameof(width) : nameof(height));

        if (Width == width && Height == height)
            return;

        Width = width;
        Height = height;
        ColorBuffer = new uint[width * height];
        DepthBuffer = new float[width * height];
    }

    public void Clear(Color color)
    {
        Array.Fill(ColorBuffer, PackColor(color));
        _depthDirty = true;
    }

    private bool _depthDirty;

    public void EnsureDepthCleared()
    {
        if (!_depthDirty) return;
        _depthDirty = false;
        Array.Fill(DepthBuffer, float.PositiveInfinity);
    }

    public static uint PackColor(Color color)
    {
        return (uint)(color.B | (color.G << 8) | (color.R << 16) | (color.A << 24));
    }
}