using AssemblyEngine.Core;
using System.Buffers.Binary;

namespace VisualNovelSample;

internal static class VisualNovelAssetBuilder
{
    public static void EnsureAssets(string assetDirectory)
    {
        Directory.CreateDirectory(assetDirectory);

        WriteBitmap(Path.Combine(assetDirectory, "backdrop-base.bmp"), BuildBackdropBase());
        WriteBitmap(Path.Combine(assetDirectory, "backdrop-mid.bmp"), BuildBackdropMid());
        WriteBitmap(Path.Combine(assetDirectory, "backdrop-front.bmp"), BuildBackdropFront());
        WriteBitmap(Path.Combine(assetDirectory, "iris-idle.bmp"), BuildPortrait(CharacterKind.Iris, PortraitFrame.Idle));
        WriteBitmap(Path.Combine(assetDirectory, "iris-talk.bmp"), BuildPortrait(CharacterKind.Iris, PortraitFrame.Talk));
        WriteBitmap(Path.Combine(assetDirectory, "iris-blink.bmp"), BuildPortrait(CharacterKind.Iris, PortraitFrame.Blink));
        WriteBitmap(Path.Combine(assetDirectory, "rowan-idle.bmp"), BuildPortrait(CharacterKind.Rowan, PortraitFrame.Idle));
        WriteBitmap(Path.Combine(assetDirectory, "rowan-talk.bmp"), BuildPortrait(CharacterKind.Rowan, PortraitFrame.Talk));
        WriteBitmap(Path.Combine(assetDirectory, "rowan-blink.bmp"), BuildPortrait(CharacterKind.Rowan, PortraitFrame.Blink));
    }

    private static PixelCanvas BuildBackdropBase()
    {
        var canvas = new PixelCanvas(VisualNovelScene.ViewportWidth, VisualNovelScene.ViewportHeight, Color.Black);
        var top = new Color(9, 16, 38);
        var middle = new Color(28, 35, 66);
        var bottom = new Color(88, 62, 78);

        for (var y = 0; y < canvas.Height; y++)
        {
            var t = y / (float)(canvas.Height - 1);
            var band = t < 0.62f
                ? Lerp(top, middle, t / 0.62f)
                : Lerp(middle, bottom, (t - 0.62f) / 0.38f);

            for (var x = 0; x < canvas.Width; x++)
                canvas.SetPixel(x, y, band);
        }

        canvas.FillCircle(1038, 132, 64, new Color(246, 236, 195));
        canvas.FillCircle(1038, 132, 48, new Color(255, 247, 225));
        canvas.FillCircle(996, 110, 12, new Color(226, 208, 170));

        var starRandom = new Random(8401);
        for (var index = 0; index < 160; index++)
        {
            var x = starRandom.Next(32, canvas.Width - 32);
            var y = starRandom.Next(20, 286);
            var radius = starRandom.Next(0, 2) == 0 ? 1 : 2;
            var starColor = starRandom.Next(0, 3) switch
            {
                0 => new Color(255, 250, 236),
                1 => new Color(182, 230, 255),
                _ => new Color(255, 213, 182)
            };
            canvas.FillCircle(x, y, radius, starColor);
        }

        for (var x = 0; x < canvas.Width; x++)
        {
            var ridge = 132f
                + (MathF.Sin(x * 0.0086f) * 18f)
                + (MathF.Sin((x * 0.021f) + 1.7f) * 28f)
                + (MathF.Sin((x * 0.0042f) + 0.5f) * 10f);
            var startY = canvas.Height - (int)MathF.Round(ridge);
            for (var y = Math.Max(0, startY); y < canvas.Height; y++)
                canvas.SetPixel(x, y, new Color(18, 19, 34));
        }

        var skylineRandom = new Random(1190);
        for (var x = 84; x < canvas.Width - 84;)
        {
            var width = skylineRandom.Next(28, 74);
            var height = skylineRandom.Next(48, 162);
            var baseY = canvas.Height - 74 - skylineRandom.Next(0, 22);
            canvas.FillRect(x, baseY - height, width, height, new Color(27, 26, 48));

            for (var windowY = baseY - height + 14; windowY < baseY - 12; windowY += 18)
            {
                for (var windowX = x + 8; windowX < x + width - 10; windowX += 16)
                {
                    if (((windowX + windowY) / 11) % 3 == 0)
                        continue;

                    var windowColor = ((windowX + windowY) % 5 == 0)
                        ? new Color(255, 215, 139)
                        : new Color(184, 225, 255);
                    canvas.FillRect(windowX, windowY, 6, 9, windowColor);
                }
            }

            x += width + skylineRandom.Next(6, 18);
        }

        canvas.DrawLine(0, 480, canvas.Width - 1, 480, 2, new Color(214, 145, 135));
        return canvas;
    }

    private static PixelCanvas BuildBackdropMid()
    {
        var canvas = new PixelCanvas(VisualNovelScene.ViewportWidth, VisualNovelScene.ViewportHeight, Color.Transparent);

        DrawCloud(canvas, 212, 132, 70, 28, new Color(78, 96, 138));
        DrawCloud(canvas, 438, 178, 88, 34, new Color(87, 108, 152));
        DrawCloud(canvas, 744, 118, 96, 36, new Color(94, 118, 165));
        DrawCloud(canvas, 1016, 204, 116, 40, new Color(89, 104, 150));

        DrawLanternString(canvas, 144, 54, 280, 112, new Color(255, 205, 108), new Color(107, 68, 40));
        DrawLanternString(canvas, 746, 74, 248, 124, new Color(255, 188, 126), new Color(108, 72, 46));
        return canvas;
    }

    private static PixelCanvas BuildBackdropFront()
    {
        var canvas = new PixelCanvas(VisualNovelScene.ViewportWidth, VisualNovelScene.ViewportHeight, Color.Transparent);
        var branch = new Color(25, 20, 33);
        var bloomA = new Color(147, 88, 122);
        var bloomB = new Color(98, 135, 132);

        canvas.FillRect(0, 642, canvas.Width, 18, branch);
        canvas.FillRect(0, 660, canvas.Width, 60, new Color(16, 12, 22));

        DrawBranchCluster(canvas, 0, 34, branch, bloomA, bloomB);
        DrawBranchCluster(canvas, 984, 28, branch, bloomA, bloomB);
        DrawLeafSpray(canvas, 182, 616, branch, bloomA);
        DrawLeafSpray(canvas, 1058, 622, branch, bloomB);
        return canvas;
    }

    private static PixelCanvas BuildPortrait(CharacterKind kind, PortraitFrame frame)
    {
        var palette = kind == CharacterKind.Iris ? CharacterPalette.Iris : CharacterPalette.Rowan;
        var canvas = new PixelCanvas(360, 560, Color.Transparent);

        canvas.FillEllipse(184, 518, 98, 18, new Color(10, 12, 18));
        canvas.FillEllipse(182, 202, 118, 132, palette.HairBack);
        canvas.FillEllipse(kind == CharacterKind.Iris ? 114 : 124, 270, 40, 126, palette.HairBack);
        canvas.FillEllipse(kind == CharacterKind.Iris ? 248 : 238, 278, 46, 138, palette.HairBack);

        canvas.FillRoundedRect(102, 262, 156, 198, 28, palette.OuterClothing);
        canvas.FillRoundedRect(126, 274, 108, 184, 20, palette.InnerClothing);
        canvas.FillRect(154, 224, 26, 34, palette.Skin);
        canvas.FillRect(180, 224, 26, 34, palette.Skin);
        canvas.FillCircle(180, 174, 74, palette.Skin);
        canvas.FillEllipse(180, 138, 118, 72, palette.HairFront);
        canvas.FillEllipse(180, 172, 94, 42, palette.HairFront);

        if (kind == CharacterKind.Iris)
        {
            canvas.FillEllipse(264, 128, 18, 24, palette.AccentPrimary);
            canvas.FillEllipse(242, 124, 16, 20, palette.AccentPrimary);
            canvas.FillCircle(254, 130, 6, new Color(255, 245, 255));
            canvas.FillEllipse(180, 332, 30, 52, palette.AccentSecondary);
        }
        else
        {
            canvas.FillRoundedRect(132, 300, 96, 34, 12, palette.AccentPrimary);
            canvas.FillRoundedRect(138, 332, 84, 28, 10, palette.AccentSecondary);
        }

        DrawEyes(canvas, frame, palette);
        DrawMouth(canvas, frame, palette);
        canvas.FillEllipse(145, 194, 12, 6, palette.Blush);
        canvas.FillEllipse(215, 194, 12, 6, palette.Blush);
        canvas.DrawLine(132, 438, 228, 438, 3, palette.Trim);
        canvas.DrawLine(154, 258, 180, 318, 3, palette.Trim);
        canvas.DrawLine(206, 258, 180, 318, 3, palette.Trim);
        return canvas;
    }

    private static void DrawCloud(PixelCanvas canvas, int x, int y, int radiusX, int radiusY, Color color)
    {
        canvas.FillEllipse(x, y, radiusX, radiusY, color);
        canvas.FillEllipse(x - 42, y + 8, radiusX - 18, radiusY - 6, color);
        canvas.FillEllipse(x + 52, y + 10, radiusX - 22, radiusY - 8, color);
    }

    private static void DrawLanternString(PixelCanvas canvas, int startX, int topY, int width, int depth, Color lanternColor, Color stringColor)
    {
        canvas.DrawLine(startX, topY, startX + width, topY + 18, 2, stringColor);

        for (var index = 0; index < 4; index++)
        {
            var x = startX + 28 + (index * (width / 4));
            var stringLength = 26 + (index * 8);
            canvas.DrawLine(x, topY + 4, x, topY + stringLength, 2, stringColor);
            canvas.FillRoundedRect(x - 12, topY + stringLength, 24, 32, 8, lanternColor);
            canvas.FillRect(x - 8, topY + stringLength + 6, 16, 4, new Color(120, 74, 42));
            canvas.FillRect(x - 8, topY + stringLength + 18, 16, 4, new Color(120, 74, 42));
        }
    }

    private static void DrawBranchCluster(PixelCanvas canvas, int x, int y, Color branch, Color bloomA, Color bloomB)
    {
        canvas.DrawLine(x, y, x + 180, y + 84, 8, branch);
        canvas.DrawLine(x + 86, y + 34, x + 166, y + 2, 6, branch);
        canvas.DrawLine(x + 124, y + 60, x + 212, y + 126, 5, branch);

        for (var index = 0; index < 9; index++)
        {
            var bloomX = x + 52 + (index * 18);
            var bloomY = y + ((index % 2 == 0) ? 34 : 66);
            canvas.FillCircle(bloomX, bloomY, 8, index % 2 == 0 ? bloomA : bloomB);
            canvas.FillCircle(bloomX + 6, bloomY + 8, 6, index % 2 == 0 ? bloomB : bloomA);
        }
    }

    private static void DrawLeafSpray(PixelCanvas canvas, int x, int y, Color branch, Color bloom)
    {
        canvas.DrawLine(x, y, x + 72, y - 36, 5, branch);
        canvas.DrawLine(x + 20, y - 10, x + 40, y - 56, 4, branch);
        canvas.DrawLine(x + 40, y - 18, x + 96, y - 72, 4, branch);

        for (var index = 0; index < 7; index++)
        {
            var leafX = x + (index * 16);
            var leafY = y - 18 - ((index % 3) * 12);
            canvas.FillEllipse(leafX, leafY, 12, 6, bloom);
        }
    }

    private static void DrawEyes(PixelCanvas canvas, PortraitFrame frame, CharacterPalette palette)
    {
        if (frame == PortraitFrame.Blink)
        {
            canvas.DrawLine(142, 174, 164, 178, 3, palette.Eye);
            canvas.DrawLine(196, 178, 218, 174, 3, palette.Eye);
            return;
        }

        canvas.FillEllipse(152, 176, 12, 8, palette.EyeWhite);
        canvas.FillEllipse(208, 176, 12, 8, palette.EyeWhite);
        canvas.FillCircle(152, 176, 5, palette.Eye);
        canvas.FillCircle(208, 176, 5, palette.Eye);
        canvas.FillCircle(154, 174, 2, Color.White);
        canvas.FillCircle(210, 174, 2, Color.White);
        canvas.DrawLine(138, 160, 166, 164, 3, palette.Eyelash);
        canvas.DrawLine(194, 164, 222, 160, 3, palette.Eyelash);
    }

    private static void DrawMouth(PixelCanvas canvas, PortraitFrame frame, CharacterPalette palette)
    {
        switch (frame)
        {
            case PortraitFrame.Talk:
                canvas.FillEllipse(180, 224, 12, 8, palette.Mouth);
                canvas.FillEllipse(180, 226, 6, 4, palette.MouthHighlight);
                break;
            default:
                canvas.DrawLine(170, 222, 190, 224, 2, palette.Mouth);
                break;
        }
    }

    private static Color Lerp(Color from, Color to, float amount)
    {
        var t = Math.Clamp(amount, 0f, 1f);
        return new Color(
            (byte)(from.R + ((to.R - from.R) * t)),
            (byte)(from.G + ((to.G - from.G) * t)),
            (byte)(from.B + ((to.B - from.B) * t)),
            (byte)(from.A + ((to.A - from.A) * t)));
    }

    private static void WriteBitmap(string path, PixelCanvas canvas)
    {
        const int fileHeaderSize = 14;
        const int dibHeaderSize = 40;
        const int pixelOffset = fileHeaderSize + dibHeaderSize;
        var imageSize = canvas.Width * canvas.Height * 4;
        var data = new byte[pixelOffset + imageSize];

        data[0] = (byte)'B';
        data[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(2, 4), data.Length);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(10, 4), pixelOffset);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(14, 4), dibHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(18, 4), canvas.Width);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(22, 4), -canvas.Height);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(26, 2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(28, 2), 32);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(30, 4), 0);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(34, 4), imageSize);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(38, 4), 2835);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(42, 4), 2835);

        Buffer.BlockCopy(canvas.Pixels, 0, data, pixelOffset, imageSize);
        File.WriteAllBytes(path, data);
    }

    private enum CharacterKind
    {
        Iris,
        Rowan
    }

    private enum PortraitFrame
    {
        Idle,
        Talk,
        Blink
    }

    private readonly record struct CharacterPalette(
        Color HairBack,
        Color HairFront,
        Color Skin,
        Color OuterClothing,
        Color InnerClothing,
        Color AccentPrimary,
        Color AccentSecondary,
        Color Eye,
        Color EyeWhite,
        Color Eyelash,
        Color Mouth,
        Color MouthHighlight,
        Color Blush,
        Color Trim)
    {
        public static CharacterPalette Iris { get; } = new(
            HairBack: new Color(78, 130, 156),
            HairFront: new Color(117, 185, 201),
            Skin: new Color(245, 220, 204),
            OuterClothing: new Color(57, 49, 84),
            InnerClothing: new Color(231, 240, 255),
            AccentPrimary: new Color(255, 151, 178),
            AccentSecondary: new Color(146, 214, 204),
            Eye: new Color(48, 96, 132),
            EyeWhite: new Color(248, 251, 255),
            Eyelash: new Color(38, 56, 70),
            Mouth: new Color(154, 82, 96),
            MouthHighlight: new Color(228, 146, 160),
            Blush: new Color(236, 186, 188),
            Trim: new Color(196, 214, 235));

        public static CharacterPalette Rowan { get; } = new(
            HairBack: new Color(114, 76, 62),
            HairFront: new Color(160, 106, 82),
            Skin: new Color(236, 209, 192),
            OuterClothing: new Color(48, 59, 86),
            InnerClothing: new Color(214, 224, 236),
            AccentPrimary: new Color(212, 141, 82),
            AccentSecondary: new Color(243, 194, 121),
            Eye: new Color(94, 68, 44),
            EyeWhite: new Color(249, 247, 243),
            Eyelash: new Color(65, 43, 28),
            Mouth: new Color(144, 88, 80),
            MouthHighlight: new Color(214, 150, 132),
            Blush: new Color(226, 182, 168),
            Trim: new Color(194, 205, 226));
    }

    private sealed class PixelCanvas
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Pixels { get; }

        public PixelCanvas(int width, int height, Color fill)
        {
            Width = width;
            Height = height;
            Pixels = new byte[width * height * 4];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                    SetPixel(x, y, fill);
            }
        }

        public void SetPixel(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return;

            var offset = ((y * Width) + x) * 4;
            Pixels[offset] = color.B;
            Pixels[offset + 1] = color.G;
            Pixels[offset + 2] = color.R;
            Pixels[offset + 3] = color.A;
        }

        public void FillRect(int x, int y, int width, int height, Color color)
        {
            var startX = Math.Max(0, x);
            var startY = Math.Max(0, y);
            var endX = Math.Min(Width, x + width);
            var endY = Math.Min(Height, y + height);

            for (var row = startY; row < endY; row++)
            {
                for (var col = startX; col < endX; col++)
                    SetPixel(col, row, color);
            }
        }

        public void FillCircle(int centerX, int centerY, int radius, Color color)
        {
            var radiusSquared = radius * radius;
            for (var y = centerY - radius; y <= centerY + radius; y++)
            {
                for (var x = centerX - radius; x <= centerX + radius; x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;
                    if ((dx * dx) + (dy * dy) <= radiusSquared)
                        SetPixel(x, y, color);
                }
            }
        }

        public void FillEllipse(int centerX, int centerY, int radiusX, int radiusY, Color color)
        {
            var rx = radiusX * radiusX;
            var ry = radiusY * radiusY;
            var threshold = rx * ry;
            for (var y = centerY - radiusY; y <= centerY + radiusY; y++)
            {
                for (var x = centerX - radiusX; x <= centerX + radiusX; x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;
                    if ((dx * dx * ry) + (dy * dy * rx) <= threshold)
                        SetPixel(x, y, color);
                }
            }
        }

        public void FillRoundedRect(int x, int y, int width, int height, int radius, Color color)
        {
            var innerWidth = Math.Max(0, width - (radius * 2));
            var innerHeight = Math.Max(0, height - (radius * 2));

            FillRect(x + radius, y, innerWidth, height, color);
            FillRect(x, y + radius, radius, innerHeight, color);
            FillRect(x + width - radius, y + radius, radius, innerHeight, color);
            FillCircle(x + radius, y + radius, radius, color);
            FillCircle(x + width - radius, y + radius, radius, color);
            FillCircle(x + radius, y + height - radius, radius, color);
            FillCircle(x + width - radius, y + height - radius, radius, color);
        }

        public void DrawLine(int x1, int y1, int x2, int y2, int thickness, Color color)
        {
            var steps = Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
            if (steps == 0)
            {
                FillCircle(x1, y1, Math.Max(1, thickness / 2), color);
                return;
            }

            var radius = Math.Max(1, thickness / 2);
            for (var step = 0; step <= steps; step++)
            {
                var t = step / (float)steps;
                var x = (int)MathF.Round(x1 + ((x2 - x1) * t));
                var y = (int)MathF.Round(y1 + ((y2 - y1) * t));
                FillCircle(x, y, radius, color);
            }
        }
    }
}