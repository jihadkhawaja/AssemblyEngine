using AssemblyEngine.Core;

namespace AssemblyEngine.UI;

/// <summary>
/// Parsed CSS style properties applicable to a UIElement.
/// Supports a subset of CSS relevant for game UI overlays.
/// </summary>
public sealed class UIStyle
{
    // Position & sizing
    public string Position { get; set; } = "static";    // static, absolute, relative
    public CssValue Left { get; set; } = CssValue.Auto;
    public CssValue Top { get; set; } = CssValue.Auto;
    public CssValue Right { get; set; } = CssValue.Auto;
    public CssValue Bottom { get; set; } = CssValue.Auto;
    public CssValue Width { get; set; } = CssValue.Auto;
    public CssValue Height { get; set; } = CssValue.Auto;

    // Margin & padding
    public int MarginTop { get; set; }
    public int MarginRight { get; set; }
    public int MarginBottom { get; set; }
    public int MarginLeft { get; set; }
    public int PaddingTop { get; set; }
    public int PaddingRight { get; set; }
    public int PaddingBottom { get; set; }
    public int PaddingLeft { get; set; }

    // Visual
    public Color BackgroundColor { get; set; } = Color.Transparent;
    public Color BorderColor { get; set; } = Color.Transparent;
    public int BorderWidth { get; set; }
    public Color TextColor { get; set; } = Color.White;
    public int FontSize { get; set; } = 16;

    // Layout
    public string Display { get; set; } = "block";      // block, none, flex
    public string FlexDirection { get; set; } = "row";
    public string AlignItems { get; set; } = "stretch";
    public string JustifyContent { get; set; } = "start";
    public int Gap { get; set; }

    // Visibility
    public bool Visible { get; set; } = true;
    public float Opacity { get; set; } = 1f;

    // Tracks which properties were explicitly set by a CSS rule.
    internal HashSet<string>? _setProps;

    internal void MarkSet(string prop)
    {
        _setProps ??= [];
        _setProps.Add(prop);
    }

    /// <summary>
    /// Copies only explicitly-set properties from <paramref name="source"/> into this style.
    /// </summary>
    internal void MergeFrom(UIStyle source)
    {
        if (source._setProps is null) return;

        foreach (var prop in source._setProps)
        {
            switch (prop)
            {
                case "position": Position = source.Position; break;
                case "display": Display = source.Display; break;
                case "left": Left = source.Left; break;
                case "top": Top = source.Top; break;
                case "right": Right = source.Right; break;
                case "bottom": Bottom = source.Bottom; break;
                case "width": Width = source.Width; break;
                case "height": Height = source.Height; break;
                case "margin-top": MarginTop = source.MarginTop; break;
                case "margin-right": MarginRight = source.MarginRight; break;
                case "margin-bottom": MarginBottom = source.MarginBottom; break;
                case "margin-left": MarginLeft = source.MarginLeft; break;
                case "padding-top": PaddingTop = source.PaddingTop; break;
                case "padding-right": PaddingRight = source.PaddingRight; break;
                case "padding-bottom": PaddingBottom = source.PaddingBottom; break;
                case "padding-left": PaddingLeft = source.PaddingLeft; break;
                case "background-color" or "background": BackgroundColor = source.BackgroundColor; break;
                case "border-color": BorderColor = source.BorderColor; break;
                case "border-width": BorderWidth = source.BorderWidth; break;
                case "color": TextColor = source.TextColor; break;
                case "font-size": FontSize = source.FontSize; break;
                case "flex-direction": FlexDirection = source.FlexDirection; break;
                case "align-items": AlignItems = source.AlignItems; break;
                case "justify-content": JustifyContent = source.JustifyContent; break;
                case "gap": Gap = source.Gap; break;
                case "opacity": Opacity = source.Opacity; break;
                case "visible": Visible = source.Visible; break;
            }
            MarkSet(prop);
        }
    }

    /// <summary>Creates an independent copy of this style.</summary>
    internal UIStyle Clone()
    {
        var c = new UIStyle
        {
            Position = Position,
            Display = Display,
            Left = Left,
            Top = Top,
            Right = Right,
            Bottom = Bottom,
            Width = Width,
            Height = Height,
            MarginTop = MarginTop,
            MarginRight = MarginRight,
            MarginBottom = MarginBottom,
            MarginLeft = MarginLeft,
            PaddingTop = PaddingTop,
            PaddingRight = PaddingRight,
            PaddingBottom = PaddingBottom,
            PaddingLeft = PaddingLeft,
            BackgroundColor = BackgroundColor,
            BorderColor = BorderColor,
            BorderWidth = BorderWidth,
            TextColor = TextColor,
            FontSize = FontSize,
            FlexDirection = FlexDirection,
            AlignItems = AlignItems,
            JustifyContent = JustifyContent,
            Gap = Gap,
            Visible = Visible,
            Opacity = Opacity,
        };
        if (_setProps is not null)
            c._setProps = new HashSet<string>(_setProps);
        return c;
    }
}

/// <summary>
/// A CSS length/percentage/auto value.
/// </summary>
public readonly record struct CssValue(float Amount, CssUnit Unit)
{
    public static readonly CssValue Auto = new(0, CssUnit.Auto);
    public static CssValue Px(float px) => new(px, CssUnit.Px);
    public static CssValue Percent(float pct) => new(pct, CssUnit.Percent);

    public int Resolve(int parentSize)
    {
        return Unit switch
        {
            CssUnit.Px => (int)Amount,
            CssUnit.Percent => (int)(Amount / 100f * parentSize),
            _ => 0
        };
    }

    public bool IsAuto => Unit == CssUnit.Auto;
}

public enum CssUnit
{
    Auto,
    Px,
    Percent,
}
