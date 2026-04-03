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
