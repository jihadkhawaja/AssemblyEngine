using AssemblyEngine.Core;

namespace AssemblyEngine.UI;

/// <summary>
/// Renders the UI element tree using the engine's 2D graphics primitives.
/// Text rendering uses a built-in 5x7 bitmap font.
/// </summary>
public static class UIRenderer
{
    private const int GlyphWidth = 5;
    private const int GlyphHeight = 7;
    private const int GlyphAdvance = GlyphWidth + 1;

    private static readonly IReadOnlyDictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]>
    {
        [' '] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
        ['!'] = new byte[] { 0x04, 0x04, 0x04, 0x04, 0x04, 0x00, 0x04 },
        ['-'] = new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 },
        ['.'] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x06 },
        ['/'] = new byte[] { 0x01, 0x02, 0x04, 0x08, 0x10, 0x00, 0x00 },
        ['0'] = new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E },
        ['1'] = new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E },
        ['2'] = new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F },
        ['3'] = new byte[] { 0x1E, 0x01, 0x01, 0x0E, 0x01, 0x01, 0x1E },
        ['4'] = new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 },
        ['5'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x01, 0x01, 0x1E },
        ['6'] = new byte[] { 0x0E, 0x10, 0x10, 0x1E, 0x11, 0x11, 0x0E },
        ['7'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 },
        ['8'] = new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E },
        ['9'] = new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x01, 0x0E },
        [':'] = new byte[] { 0x00, 0x04, 0x04, 0x00, 0x04, 0x04, 0x00 },
        ['?'] = new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x00, 0x04 },
        ['A'] = new byte[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
        ['B'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E },
        ['C'] = new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E },
        ['D'] = new byte[] { 0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E },
        ['E'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F },
        ['F'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10 },
        ['G'] = new byte[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0E },
        ['H'] = new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
        ['I'] = new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E },
        ['J'] = new byte[] { 0x01, 0x01, 0x01, 0x01, 0x11, 0x11, 0x0E },
        ['K'] = new byte[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 },
        ['L'] = new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F },
        ['M'] = new byte[] { 0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11 },
        ['N'] = new byte[] { 0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11 },
        ['O'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
        ['P'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 },
        ['Q'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D },
        ['R'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 },
        ['S'] = new byte[] { 0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E },
        ['T'] = new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 },
        ['U'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
        ['V'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 },
        ['W'] = new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x15, 0x0A },
        ['X'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 },
        ['Y'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 },
        ['Z'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F },
        ['|'] = new byte[] { 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 },
    };

    public static void Render(UIElement element)
    {
        var style = element.ComputedStyle;
        if (!style.Visible || style.Display == "none") return;

        int x = element.LayoutX;
        int y = element.LayoutY;
        int w = element.LayoutWidth;
        int h = element.LayoutHeight;

        // Background
        if (style.BackgroundColor.A > 0 && w > 0 && h > 0)
            Graphics.DrawFilledRect(x, y, w, h, style.BackgroundColor);

        // Border
        if (style.BorderWidth > 0 && style.BorderColor.A > 0 && w > 0 && h > 0)
        {
            var bc = style.BorderColor;
            int bw = style.BorderWidth;
            // Top
            Graphics.DrawFilledRect(x, y, w, bw, bc);
            // Bottom
            Graphics.DrawFilledRect(x, y + h - bw, w, bw, bc);
            // Left
            Graphics.DrawFilledRect(x, y, bw, h, bc);
            // Right
            Graphics.DrawFilledRect(x + w - bw, y, bw, h, bc);
        }

        // Text
        if (element.Tag == "#text" && element.Text.Length > 0)
        {
            DrawText(element.Text, x, y,
                element.Parent?.ComputedStyle.TextColor ?? Color.White,
                element.Parent?.ComputedStyle.FontSize ?? 16);
        }

        // Children
        foreach (var child in element.Children)
            Render(child);
    }

    private static void DrawText(string text, int x, int y, Color color, int fontSize)
    {
        int scale = Math.Max(1, (fontSize + 4) / 12);
        int advanceX = GlyphAdvance * scale;
        int lineHeight = GlyphHeight * scale + scale;
        int cursorX = x;
        int cursorY = y;

        foreach (char character in text)
        {
            if (character == '\n')
            {
                cursorX = x;
                cursorY += lineHeight;
                continue;
            }

            if (!TryGetGlyph(character, out var glyph))
                glyph = Glyphs['?'];

            DrawGlyph(glyph, cursorX, cursorY, scale, color);
            cursorX += advanceX;
        }
    }

    private static bool TryGetGlyph(char character, out byte[] glyph)
    {
        if (Glyphs.TryGetValue(character, out glyph!))
            return true;

        return Glyphs.TryGetValue(char.ToUpperInvariant(character), out glyph!);
    }

    private static void DrawGlyph(byte[] glyph, int x, int y, int scale, Color color)
    {
        for (int row = 0; row < GlyphHeight; row++)
        {
            byte bits = glyph[row];
            for (int column = 0; column < GlyphWidth; column++)
            {
                int mask = 1 << (GlyphWidth - 1 - column);
                if ((bits & mask) == 0)
                    continue;

                Graphics.DrawFilledRect(
                    x + column * scale,
                    y + row * scale,
                    scale,
                    scale,
                    color);
            }
        }
    }
}
