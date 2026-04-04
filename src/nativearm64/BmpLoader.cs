using System.Buffers.Binary;

namespace AssemblyEngine.NativeArm64;

internal static class BmpLoader
{
    public static bool TryLoad(string path, out SpriteAsset? sprite)
    {
        sprite = null;

        if (!File.Exists(path))
            return false;

        var data = File.ReadAllBytes(path);
        if (data.Length < 54 || data[0] != (byte)'B' || data[1] != (byte)'M')
            return false;

        var pixelOffset = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(10, 4));
        var width = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(18, 4));
        var signedHeight = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(22, 4));
        var bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(28, 2));
        var compression = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(30, 4));

        if (width <= 0 || signedHeight == 0 || compression != 0 || (bitsPerPixel != 24 && bitsPerPixel != 32))
            return false;

        var height = Math.Abs(signedHeight);
        var bytesPerPixel = bitsPerPixel / 8;
        var rowStride = ((width * bytesPerPixel) + 3) & ~3;
        var requiredBytes = pixelOffset + (rowStride * height);
        if (pixelOffset < 0 || requiredBytes > data.Length)
            return false;

        var pixels = new byte[width * height * 4];
        var bottomUp = signedHeight > 0;

        for (var y = 0; y < height; y++)
        {
            var sourceY = bottomUp ? (height - 1 - y) : y;
            var sourceRow = pixelOffset + (sourceY * rowStride);
            var destRow = y * width * 4;

            for (var x = 0; x < width; x++)
            {
                var source = sourceRow + (x * bytesPerPixel);
                var dest = destRow + (x * 4);
                pixels[dest] = data[source];
                pixels[dest + 1] = data[source + 1];
                pixels[dest + 2] = data[source + 2];
                pixels[dest + 3] = bitsPerPixel == 32 ? data[source + 3] : (byte)255;
            }
        }

        sprite = new SpriteAsset
        {
            Pixels = pixels,
            Width = width,
            Height = height,
            Pitch = width * 4,
            HasAlpha = bitsPerPixel == 32
        };

        return true;
    }
}