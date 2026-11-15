using System.Text;

namespace HuntersAccord.Core.Steam;

/// <summary>Minimal reader for Valve's text KeyValues (VDF) format, e.g. libraryfolders.vdf / appmanifest_*.acf.</summary>
public static class VdfReader
{
    public sealed class Node
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Node> Children { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? Value(string key) => Values.GetValueOrDefault(key);
        public Node? Child(string key) => Children.GetValueOrDefault(key);
    }

    public static Node Parse(string text)
    {
        var tokens = Tokenize(text).GetEnumerator();
        var root = new Node();
        ParseInto(root, tokens, topLevel: true);
        return root;
    }

    private static void ParseInto(Node node, IEnumerator<string?> tokens, bool topLevel)
    {
        while (tokens.MoveNext())
        {
            var key = tokens.Current;
            if (key is null) // '}'
            {
                if (topLevel) continue;
                return;
            }
            if (!tokens.MoveNext()) return;
            var next = tokens.Current;
            if (next == "{")
            {
                var child = new Node();
                ParseInto(child, tokens, topLevel: false);
                node.Children[key] = child;
            }
            else if (next is not null)
            {
                node.Values[key] = next;
            }
        }
    }

    /// <summary>Yields quoted strings, "{" for an opening brace, and null for a closing brace.</summary>
    private static IEnumerable<string?> Tokenize(string text)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '{': yield return "{"; break;
                case '}': yield return null; break;
                case '/' when i + 1 < text.Length && text[i + 1] == '/':
                    while (i < text.Length && text[i] != '\n') i++;
                    break;
                case '"':
                    sb.Clear();
                    for (i++; i < text.Length && text[i] != '"'; i++)
                    {
                        if (text[i] == '\\' && i + 1 < text.Length)
                        {
                            i++;
                            sb.Append(text[i] switch { 'n' => '\n', 't' => '\t', _ => text[i] });
                        }
                        else sb.Append(text[i]);
                    }
                    yield return sb.ToString();
                    break;
            }
        }
    }
}
