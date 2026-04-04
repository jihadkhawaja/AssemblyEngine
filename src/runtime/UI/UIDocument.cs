namespace AssemblyEngine.UI;

/// <summary>
/// A parsed UI document combining HTML elements with CSS styles.
/// Provides the entry point for UI rendering each frame.
/// </summary>
public sealed class UIDocument
{
    public UIElement Root { get; }
    public Dictionary<string, UIStyle> Styles { get; }
    public float RenderScale { get; set; } = 1f;

    private UIDocument(UIElement root, Dictionary<string, UIStyle> styles)
    {
        Root = root;
        Styles = styles;
        ApplyStyles(root);
    }

    public static UIDocument Parse(string html, string? css = null)
    {
        var root = HtmlParser.Parse(html);
        var styles = css is not null ? CssParser.Parse(css) : [];
        return new UIDocument(root, styles);
    }

    public UIElement? FindById(string id) => Root.FindById(id);
    public List<UIElement> FindByClass(string cls) => Root.FindByClass(cls);

    public void UpdateText(string id, string text)
    {
        var el = FindById(id);
        if (el is null) return;

        // Clear existing text children and set new text
        el.Children.RemoveAll(c => c.Tag == "#text");
        el.Children.Add(new UIElement { Tag = "#text", Text = text, Parent = el });
    }

    public void SetVisible(string id, bool visible)
    {
        var el = FindById(id);
        if (el is not null)
            el.ComputedStyle.Visible = visible;
    }

    public void Render(int viewportWidth, int viewportHeight)
    {
        UILayoutEngine.Layout(Root, 0, 0, viewportWidth, viewportHeight);
        UIRenderer.Render(Root, RenderScale);
    }

    private void ApplyStyles(UIElement element)
    {
        // Apply by tag
        if (Styles.TryGetValue(element.Tag, out var tagStyle))
            MergeStyle(element.ComputedStyle, tagStyle);

        // Apply by class
        foreach (var cls in element.Class.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Styles.TryGetValue("." + cls, out var classStyle))
                MergeStyle(element.ComputedStyle, classStyle);
        }

        // Apply by ID
        if (element.Id.Length > 0 && Styles.TryGetValue("#" + element.Id, out var idStyle))
            MergeStyle(element.ComputedStyle, idStyle);

        foreach (var child in element.Children)
            ApplyStyles(child);
    }

    private static void MergeStyle(UIStyle target, UIStyle source)
    {
        target.Position = source.Position;
        target.Display = source.Display;
        if (!source.Left.IsAuto) target.Left = source.Left;
        if (!source.Top.IsAuto) target.Top = source.Top;
        if (!source.Right.IsAuto) target.Right = source.Right;
        if (!source.Bottom.IsAuto) target.Bottom = source.Bottom;
        if (!source.Width.IsAuto) target.Width = source.Width;
        if (!source.Height.IsAuto) target.Height = source.Height;
        target.MarginTop = source.MarginTop;
        target.MarginRight = source.MarginRight;
        target.MarginBottom = source.MarginBottom;
        target.MarginLeft = source.MarginLeft;
        target.PaddingTop = source.PaddingTop;
        target.PaddingRight = source.PaddingRight;
        target.PaddingBottom = source.PaddingBottom;
        target.PaddingLeft = source.PaddingLeft;
        if (source.BackgroundColor.A > 0) target.BackgroundColor = source.BackgroundColor;
        if (source.BorderColor.A > 0) target.BorderColor = source.BorderColor;
        if (source.BorderWidth > 0) target.BorderWidth = source.BorderWidth;
        if (source.TextColor.A > 0) target.TextColor = source.TextColor;
        if (source.FontSize != 16) target.FontSize = source.FontSize;
        target.Opacity = source.Opacity;
        target.Visible = source.Visible;
        target.FlexDirection = source.FlexDirection;
        target.AlignItems = source.AlignItems;
        target.JustifyContent = source.JustifyContent;
        target.Gap = source.Gap;
    }
}
