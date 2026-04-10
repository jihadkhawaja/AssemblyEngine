using AssemblyEngine.Core;

namespace AssemblyEngine.UI;

/// <summary>
/// Minimal CSS parser. Parses stylesheets into selector-property maps.
/// Supports: tag, .class, #id selectors. Properties: common box model + colors.
/// </summary>
public static class CssParser
{
    public static Dictionary<string, UIStyle> Parse(string css)
    {
        var styles = new Dictionary<string, UIStyle>();
        var reader = new CssReader(css);

        while (!reader.IsEof)
        {
            reader.SkipWhitespace();
            if (reader.IsEof) break;

            // Skip comments
            if (reader.Peek() == '/' && reader.PeekAt(1) == '*')
            {
                reader.SkipBlockComment();
                continue;
            }

            var selector = reader.ReadUntil('{').Trim();
            if (string.IsNullOrEmpty(selector)) break;
            reader.Read(); // {

            var style = ParseDeclarations(reader);
            reader.SkipWhitespace();
            if (reader.Peek() == '}') reader.Read();

            foreach (var sel in selector.Split(','))
            {
                var s = sel.Trim();
                if (s.Length > 0)
                {
                    if (styles.TryGetValue(s, out var existing))
                        existing.MergeFrom(style);
                    else
                        styles[s] = style.Clone();
                }
            }
        }

        return styles;
    }

    private static UIStyle ParseDeclarations(CssReader reader)
    {
        var style = new UIStyle();

        while (!reader.IsEof && reader.Peek() != '}')
        {
            reader.SkipWhitespace();
            if (reader.Peek() == '}') break;

            var property = reader.ReadUntil(':').Trim().ToLowerInvariant();
            reader.Read(); // :
            reader.SkipWhitespace();
            var value = reader.ReadUntil(';').Trim();
            if (reader.Peek() == ';') reader.Read();

            ApplyProperty(style, property, value);
        }

        return style;
    }

    private static void ApplyProperty(UIStyle style, string prop, string value)
    {
        switch (prop)
        {
            case "display":
                style.Display = value;
                style.MarkSet(prop);
                break;
            case "position":
                style.Position = value;
                style.MarkSet(prop);
                break;
            case "left":
                style.Left = ParseCssValue(value);
                style.MarkSet(prop);
                break;
            case "top":
                style.Top = ParseCssValue(value);
                style.MarkSet(prop);
                break;
            case "right":
                style.Right = ParseCssValue(value);
                style.MarkSet(prop);
                break;
            case "bottom":
                style.Bottom = ParseCssValue(value);
                style.MarkSet(prop);
                break;
            case "width":
                style.Width = ParseCssValue(value);
                style.MarkSet(prop);
                break;
            case "height":
                style.Height = ParseCssValue(value);
                style.MarkSet(prop);
                break;
            case "background-color" or "background":
                style.BackgroundColor = ParseColor(value);
                style.MarkSet(prop);
                break;
            case "color":
                style.TextColor = ParseColor(value);
                style.MarkSet(prop);
                break;
            case "border-color":
                style.BorderColor = ParseColor(value);
                style.MarkSet(prop);
                break;
            case "border-width":
                style.BorderWidth = ParseInt(value);
                style.MarkSet(prop);
                break;
            case "font-size":
                style.FontSize = ParseInt(value);
                style.MarkSet(prop);
                break;
            case "opacity":
                if (float.TryParse(value, out var op))
                    style.Opacity = op;
                style.MarkSet(prop);
                break;
            case "margin":
                ParseFourSides(value,
                    v => style.MarginTop = v, v => style.MarginRight = v,
                    v => style.MarginBottom = v, v => style.MarginLeft = v);
                style.MarkSet("margin-top");
                style.MarkSet("margin-right");
                style.MarkSet("margin-bottom");
                style.MarkSet("margin-left");
                break;
            case "margin-top": style.MarginTop = ParseInt(value); style.MarkSet(prop); break;
            case "margin-right": style.MarginRight = ParseInt(value); style.MarkSet(prop); break;
            case "margin-bottom": style.MarginBottom = ParseInt(value); style.MarkSet(prop); break;
            case "margin-left": style.MarginLeft = ParseInt(value); style.MarkSet(prop); break;
            case "padding":
                ParseFourSides(value,
                    v => style.PaddingTop = v, v => style.PaddingRight = v,
                    v => style.PaddingBottom = v, v => style.PaddingLeft = v);
                style.MarkSet("padding-top");
                style.MarkSet("padding-right");
                style.MarkSet("padding-bottom");
                style.MarkSet("padding-left");
                break;
            case "padding-top": style.PaddingTop = ParseInt(value); style.MarkSet(prop); break;
            case "padding-right": style.PaddingRight = ParseInt(value); style.MarkSet(prop); break;
            case "padding-bottom": style.PaddingBottom = ParseInt(value); style.MarkSet(prop); break;
            case "padding-left": style.PaddingLeft = ParseInt(value); style.MarkSet(prop); break;
            case "flex-direction": style.FlexDirection = value; style.MarkSet(prop); break;
            case "align-items": style.AlignItems = value; style.MarkSet(prop); break;
            case "justify-content": style.JustifyContent = value; style.MarkSet(prop); break;
            case "gap": style.Gap = ParseInt(value); style.MarkSet(prop); break;
        }
    }

    private static CssValue ParseCssValue(string value)
    {
        value = value.Trim().ToLowerInvariant();
        if (value == "auto") return CssValue.Auto;
        if (value.EndsWith('%') && float.TryParse(value[..^1], out var pct))
            return CssValue.Percent(pct);
        if (value.EndsWith("px") && float.TryParse(value[..^2], out var px))
            return CssValue.Px(px);
        if (float.TryParse(value, out var num))
            return CssValue.Px(num);
        return CssValue.Auto;
    }

    private static Color ParseColor(string value)
    {
        value = value.Trim().ToLowerInvariant();
        return value switch
        {
            "transparent" => Color.Transparent,
            "black" => Color.Black,
            "white" => Color.White,
            "red" => Color.Red,
            "green" => Color.Green,
            "blue" => Color.Blue,
            "yellow" => Color.Yellow,
            _ when value.StartsWith('#') => Color.FromHex(value),
            _ when value.StartsWith("rgba(") => ParseRgba(value),
            _ when value.StartsWith("rgb(") => ParseRgb(value),
            _ => Color.Transparent
        };
    }

    private static Color ParseRgba(string v)
    {
        var inner = v["rgba(".Length..^1];
        var parts = inner.Split(',');
        if (parts.Length < 4) return Color.Transparent;
        return new Color(
            byte.Parse(parts[0].Trim()),
            byte.Parse(parts[1].Trim()),
            byte.Parse(parts[2].Trim()),
            (byte)(float.Parse(parts[3].Trim()) * 255));
    }

    private static Color ParseRgb(string v)
    {
        var inner = v["rgb(".Length..^1];
        var parts = inner.Split(',');
        if (parts.Length < 3) return Color.Transparent;
        return new Color(
            byte.Parse(parts[0].Trim()),
            byte.Parse(parts[1].Trim()),
            byte.Parse(parts[2].Trim()));
    }

    private static int ParseInt(string value)
    {
        value = value.Trim().ToLowerInvariant().Replace("px", "");
        return int.TryParse(value, out var v) ? v : 0;
    }

    private static void ParseFourSides(string value,
        Action<int> top, Action<int> right, Action<int> bottom, Action<int> left)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        switch (parts.Length)
        {
            case 1:
                var all = ParseInt(parts[0]);
                top(all); right(all); bottom(all); left(all);
                break;
            case 2:
                var tb = ParseInt(parts[0]);
                var lr = ParseInt(parts[1]);
                top(tb); bottom(tb); right(lr); left(lr);
                break;
            case 4:
                top(ParseInt(parts[0]));
                right(ParseInt(parts[1]));
                bottom(ParseInt(parts[2]));
                left(ParseInt(parts[3]));
                break;
        }
    }
}

internal sealed class CssReader
{
    private readonly string _source;
    private int _pos;

    public CssReader(string source) => _source = source;
    public bool IsEof => _pos >= _source.Length;
    public char Peek() => IsEof ? '\0' : _source[_pos];
    public char PeekAt(int offset) =>
        _pos + offset < _source.Length ? _source[_pos + offset] : '\0';
    public char Read() => IsEof ? '\0' : _source[_pos++];

    public void SkipWhitespace()
    {
        while (!IsEof && char.IsWhiteSpace(_source[_pos])) _pos++;
    }

    public void SkipBlockComment()
    {
        _pos += 2; // skip /*
        var end = _source.IndexOf("*/", _pos, StringComparison.Ordinal);
        _pos = end >= 0 ? end + 2 : _source.Length;
    }

    public string ReadUntil(char stop)
    {
        int start = _pos;
        while (!IsEof && Peek() != stop) _pos++;
        return _source[start.._pos];
    }
}
