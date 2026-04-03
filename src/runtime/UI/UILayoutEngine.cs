namespace AssemblyEngine.UI;

/// <summary>
/// Simple layout engine for the UI tree. Computes LayoutX/Y/Width/Height
/// for each element based on its style and parent constraints.
/// </summary>
public static class UILayoutEngine
{
    public static void Layout(UIElement element, int x, int y, int maxW, int maxH)
    {
        var style = element.ComputedStyle;
        if (!style.Visible || style.Display == "none") return;

        // Resolve size
        int w = style.Width.IsAuto ? maxW : style.Width.Resolve(maxW);
        int h = style.Height.IsAuto ? 0 : style.Height.Resolve(maxH);

        // Apply margins
        int mx = x + style.MarginLeft;
        int my = y + style.MarginTop;

        // Position
        if (style.Position == "absolute")
        {
            if (!style.Left.IsAuto) mx = style.Left.Resolve(maxW);
            if (!style.Top.IsAuto) my = style.Top.Resolve(maxH);
            if (!style.Right.IsAuto && style.Left.IsAuto)
                mx = maxW - w - style.Right.Resolve(maxW);
            if (!style.Bottom.IsAuto && style.Top.IsAuto)
                my = maxH - h - style.Bottom.Resolve(maxH);
        }

        element.LayoutX = mx;
        element.LayoutY = my;

        // Layout children
        int contentX = mx + style.PaddingLeft;
        int contentY = my + style.PaddingTop;
        int contentW = w - style.PaddingLeft - style.PaddingRight;
        int contentH = h > 0 ? h - style.PaddingTop - style.PaddingBottom : maxH;

        if (style.Display == "flex")
            LayoutFlex(element, contentX, contentY, contentW, contentH);
        else
            LayoutBlock(element, contentX, contentY, contentW, contentH);

        // Auto height: compute from children
        if (style.Height.IsAuto && element.Children.Count > 0)
        {
            int maxBottom = 0;
            foreach (var child in element.Children)
            {
                int childBottom = child.LayoutY + child.LayoutHeight - my;
                if (childBottom > maxBottom) maxBottom = childBottom;
            }
            h = maxBottom + style.PaddingBottom;
        }

        element.LayoutWidth = w;
        element.LayoutHeight = h > 0 ? h : 0;
    }

    private static void LayoutBlock(UIElement el, int x, int y, int w, int h)
    {
        int cursorY = y;
        foreach (var child in el.Children)
        {
            if (child.ComputedStyle.Position == "absolute")
            {
                Layout(child, x, y, w, h);
                continue;
            }

            Layout(child, x, cursorY, w, h);
            cursorY += child.LayoutHeight + child.ComputedStyle.MarginBottom;
        }
    }

    private static void LayoutFlex(UIElement el, int x, int y, int w, int h)
    {
        var style = el.ComputedStyle;
        bool isRow = style.FlexDirection == "row";
        int cursor = isRow ? x : y;
        int size = isRow ? w : h;

        foreach (var child in el.Children)
        {
            if (child.ComputedStyle.Position == "absolute")
            {
                Layout(child, x, y, w, h);
                continue;
            }

            if (isRow)
            {
                Layout(child, cursor, y, size, h);
                cursor += child.LayoutWidth + style.Gap;
            }
            else
            {
                Layout(child, x, cursor, w, size);
                cursor += child.LayoutHeight + style.Gap;
            }
        }
    }
}
