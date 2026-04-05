using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace AssemblyEngine.Rendering;

[SupportedOSPlatform("windows")]
internal sealed class TextureStore
{
    private readonly List<Texture2D> _textures = [];
    private readonly Dictionary<string, int> _pathToId = new(StringComparer.OrdinalIgnoreCase);

    public int Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return -1;

        var fullPath = Path.GetFullPath(path);
        if (_pathToId.TryGetValue(fullPath, out var existingId))
            return existingId;

        try
        {
            var texture = Texture2D.Load(fullPath);
            _textures.Add(texture);
            var id = _textures.Count - 1;
            _pathToId[fullPath] = id;
            return id;
        }
        catch
        {
            return -1;
        }
    }

    public Texture2D? Get(int id)
    {
        return (uint)id < (uint)_textures.Count ? _textures[id] : null;
    }
}

[SupportedOSPlatform("windows")]
internal sealed class Texture2D
{
    public required byte[] Pixels { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public int Pitch => Width * 4;

    public static Texture2D Load(string path)
    {
        using var source = new Bitmap(path);
        using var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var context = System.Drawing.Graphics.FromImage(bitmap))
            context.DrawImage(source, 0, 0, source.Width, source.Height);

        var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            var pixels = new byte[bitmap.Width * bitmap.Height * 4];
            unsafe
            {
                fixed (byte* destination = pixels)
                {
                    Buffer.MemoryCopy((void*)data.Scan0, destination, pixels.Length, pixels.Length);
                }
            }

            return new Texture2D
            {
                Pixels = pixels,
                Width = bitmap.Width,
                Height = bitmap.Height
            };
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}