namespace AssemblyEngine.UI;

/// <summary>
/// Minimal HTML parser for game UI. Parses a subset of HTML into a UIElement tree.
/// Supports: div, span, p, h1-h6, button, img, text nodes.
/// </summary>
public static class HtmlParser
{
    public static UIElement Parse(string html)
    {
        var root = new UIElement { Tag = "body" };
        var reader = new HtmlReader(html);
        ParseChildren(reader, root);
        return root;
    }

    private static void ParseChildren(HtmlReader reader, UIElement parent)
    {
        while (!reader.IsEof)
        {
            reader.SkipWhitespace();
            if (reader.IsEof) break;

            if (reader.Peek() == '<')
            {
                if (reader.PeekAt(1) == '/')
                    break; // closing tag — return to parent

                if (reader.PeekAt(1) == '!')
                {
                    reader.SkipDeclaration();
                    continue;
                }

                var element = ParseElement(reader);
                if (element is not null)
                {
                    element.Parent = parent;
                    parent.Children.Add(element);
                }
            }
            else
            {
                var text = reader.ReadUntil('<');
                text = text.Trim();
                if (text.Length > 0)
                {
                    var textNode = new UIElement
                    {
                        Tag = "#text",
                        Text = text,
                        Parent = parent
                    };
                    parent.Children.Add(textNode);
                }
            }
        }
    }

    private static UIElement? ParseElement(HtmlReader reader)
    {
        reader.Expect('<');
        var tag = reader.ReadTagName();
        if (string.IsNullOrEmpty(tag)) return null;

        var element = new UIElement { Tag = tag.ToLowerInvariant() };
        ParseAttributes(reader, element);

        reader.SkipWhitespace();

        // Self-closing tags
        if (reader.Peek() == '/')
        {
            reader.Read();
            reader.Expect('>');
            return element;
        }

        reader.Expect('>');

        // Void elements
        if (IsVoidElement(element.Tag))
            return element;

        // Parse children
        ParseChildren(reader, element);

        // Expect closing tag
        reader.SkipWhitespace();
        if (reader.Peek() == '<' && reader.PeekAt(1) == '/')
        {
            reader.Read(); // <
            reader.Read(); // /
            reader.ReadUntil('>');
            reader.Read(); // >
        }

        return element;
    }

    private static void ParseAttributes(HtmlReader reader, UIElement element)
    {
        while (!reader.IsEof)
        {
            reader.SkipWhitespace();
            char c = reader.Peek();
            if (c == '>' || c == '/') break;

            var name = reader.ReadAttributeName();
            if (string.IsNullOrEmpty(name)) break;

            string value = "";
            reader.SkipWhitespace();
            if (reader.Peek() == '=')
            {
                reader.Read(); // =
                reader.SkipWhitespace();
                value = reader.ReadAttributeValue();
            }

            element.Attributes[name.ToLowerInvariant()] = value;

            if (name.Equals("id", StringComparison.OrdinalIgnoreCase))
                element.Id = value;
            else if (name.Equals("class", StringComparison.OrdinalIgnoreCase))
                element.Class = value;
        }
    }

    private static bool IsVoidElement(string tag) =>
        tag is "br" or "hr" or "img" or "input" or "meta" or "link";
}

/// <summary>
/// Character-level reader for HTML parsing.
/// </summary>
internal sealed class HtmlReader
{
    private readonly string _source;
    private int _pos;

    public HtmlReader(string source) => _source = source;
    public bool IsEof => _pos >= _source.Length;
    public char Peek() => IsEof ? '\0' : _source[_pos];
    public char PeekAt(int offset) =>
        _pos + offset < _source.Length ? _source[_pos + offset] : '\0';

    public char Read() => IsEof ? '\0' : _source[_pos++];

    public void Expect(char c)
    {
        if (Peek() == c) _pos++;
    }

    public void SkipWhitespace()
    {
        while (!IsEof && char.IsWhiteSpace(_source[_pos])) _pos++;
    }

    public void SkipDeclaration()
    {
        if (_pos + 3 < _source.Length && _source.Substring(_pos, 4) == "<!--")
        {
            var end = _source.IndexOf("-->", _pos + 4, StringComparison.Ordinal);
            _pos = end >= 0 ? end + 3 : _source.Length;
            return;
        }

        var declarationEnd = _source.IndexOf('>', _pos + 2);
        _pos = declarationEnd >= 0 ? declarationEnd + 1 : _source.Length;
    }

    public string ReadTagName()
    {
        int start = _pos;
        while (!IsEof && !char.IsWhiteSpace(Peek()) && Peek() != '>' && Peek() != '/')
            _pos++;
        return _source[start.._pos];
    }

    public string ReadAttributeName()
    {
        int start = _pos;
        while (!IsEof && Peek() != '=' && Peek() != '>' && Peek() != '/' &&
               !char.IsWhiteSpace(Peek()))
            _pos++;
        return _source[start.._pos];
    }

    public string ReadAttributeValue()
    {
        if (Peek() == '"' || Peek() == '\'')
        {
            char quote = Read();
            int start = _pos;
            while (!IsEof && Peek() != quote) _pos++;
            string val = _source[start.._pos];
            if (!IsEof) _pos++; // skip closing quote
            return val;
        }

        int s = _pos;
        while (!IsEof && !char.IsWhiteSpace(Peek()) && Peek() != '>') _pos++;
        return _source[s.._pos];
    }

    public string ReadUntil(char stop)
    {
        int start = _pos;
        while (!IsEof && Peek() != stop) _pos++;
        return _source[start.._pos];
    }
}
