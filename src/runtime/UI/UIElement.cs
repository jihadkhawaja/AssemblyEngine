namespace AssemblyEngine.UI;

/// <summary>
/// Represents a parsed HTML element in the UI tree.
/// </summary>
public sealed class UIElement
{
    public string Tag { get; set; } = "";
    public string Id { get; set; } = "";
    public string Class { get; set; } = "";
    public string Text { get; set; } = "";
    public Dictionary<string, string> Attributes { get; } = [];
    public List<UIElement> Children { get; } = [];
    public UIElement? Parent { get; set; }
    public UIStyle ComputedStyle { get; set; } = new();

    // Layout results (computed during layout phase)
    public int LayoutX { get; set; }
    public int LayoutY { get; set; }
    public int LayoutWidth { get; set; }
    public int LayoutHeight { get; set; }

    public UIElement? FindById(string id)
    {
        if (Id == id) return this;
        foreach (var child in Children)
        {
            var found = child.FindById(id);
            if (found is not null) return found;
        }
        return null;
    }

    public List<UIElement> FindByClass(string className)
    {
        var results = new List<UIElement>();
        CollectByClass(className, results);
        return results;
    }

    public List<UIElement> FindByTag(string tag)
    {
        var results = new List<UIElement>();
        CollectByTag(tag, results);
        return results;
    }

    private void CollectByClass(string className, List<UIElement> results)
    {
        if (Class.Split(' ').Contains(className))
            results.Add(this);
        foreach (var child in Children)
            child.CollectByClass(className, results);
    }

    private void CollectByTag(string tag, List<UIElement> results)
    {
        if (Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
            results.Add(this);
        foreach (var child in Children)
            child.CollectByTag(tag, results);
    }
}
