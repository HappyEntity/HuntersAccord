using System.Globalization;

namespace HuntersAccord.Core.Trophies;

/// <summary>
/// The in-game trophy notices as the launcher leaves them for the game's overlay (the data folder's
/// <see cref="TrophyBook.ToastFolderName"/>): drawn at three frame heights (<see cref="Heights"/>), each with transparent
/// margins of <see cref="Margin"/> for the shadow and the platinum's glow. Per trophy and height:
/// <list type="bullet">
/// <item><c>trophy-toast-&lt;id&gt;-&lt;h&gt;.png</c> — the notice;</item>
/// <item><c>…-coop.png</c> — the same with the co-op line's start ("in co-op: ") and room for the partner's name, whose
/// place is in <c>…-coop.txt</c> (<see cref="CoopSlot"/>: x, top, right end, in the picture's pixels): the overlay
/// writes the name there with <see cref="GlyphsPicture"/> (the name is only known at the award);</item>
/// <item><c>…-glow.png</c>, <c>…-coop-glow.png</c> — the platinum's glow, the notice's size, laid under it.</item>
/// </list>
/// <c>toast-glyphs-&lt;h&gt;.png</c> + <c>.txt</c>: the co-op line's characters (<see cref="ToastGlyphs"/>).
/// </summary>
public static class TrophyToastFiles
{
    /// <summary>The frame heights the notices are drawn for (1080p, 1440p, 4K): the overlay takes the nearest at least as high.</summary>
    public static readonly int[] Heights = [1080, 1440, 2160];

    /// <summary>The design's scale at a height (1 at 1080 lines).</summary>
    public static double Scale(int height) => height / 1080.0;

    /// <summary>The transparent margin around the notice (32 at 1080 lines): room for the shadow and the glow.</summary>
    public static int Margin(int height) => (int)Math.Round(32 * Scale(height));

    /// <summary>The height whose pictures serve a frame of <paramref name="frameHeight"/> lines.</summary>
    public static int HeightFor(double frameHeight)
    {
        foreach (var h in Heights)
            if (frameHeight <= h + 1) return h;
        return Heights[^1];
    }

    private static string Folder(string dataRoot) => TrophyBook.ToastFolderIn(dataRoot);

    public static string Picture(string dataRoot, int id, int height, bool coop = false, bool glow = false) =>
        Path.Combine(Folder(dataRoot), $"trophy-toast-{id}-{height}{(coop ? "-coop" : "")}{(glow ? "-glow" : "")}.png");

    public static string CoopSlotFile(string dataRoot, int id, int height) =>
        Path.Combine(Folder(dataRoot), $"trophy-toast-{id}-{height}-coop.txt");

    public static string GlyphsPicture(string dataRoot, int height) => Path.Combine(Folder(dataRoot), $"toast-glyphs-{height}.png");

    public static string GlyphsInfo(string dataRoot, int height) => Path.Combine(Folder(dataRoot), $"toast-glyphs-{height}.txt");
}

/// <summary>Where the partner's name goes on a co-op notice: its left, its line's top, the room's right end (pixels).</summary>
public readonly record struct CoopSlot(int X, int Top, int Right)
{
    public string Write() => string.Create(CultureInfo.InvariantCulture, $"{X} {Top} {Right}");

    public static CoopSlot? Parse(string text)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 3 && int.TryParse(parts[0], CultureInfo.InvariantCulture, out var x)
               && int.TryParse(parts[1], CultureInfo.InvariantCulture, out var top)
               && int.TryParse(parts[2], CultureInfo.InvariantCulture, out var right) && right > x
            ? new CoopSlot(x, top, right)
            : null;
    }
}

/// <summary>One character of the co-op line's set: its cell in the glyph picture and how far the pen moves after it.</summary>
public readonly record struct ToastGlyph(int X, int Y, int Width, int Height, int Advance, int Left);

/// <summary>
/// The co-op line's characters as the launcher drew them (one cell each, white coverage in the alpha, the line's height
/// tall): the overlay writes a partner's name from them. Text form, a line per character after the first:
/// <c>lineHeight</c>, then <c>codepoint x y width height advance left</c> (the cell goes at the pen's x − left).
/// </summary>
public sealed class ToastGlyphs
{
    public int LineHeight { get; }

    private readonly Dictionary<int, ToastGlyph> _glyphs;

    public ToastGlyphs(int lineHeight, Dictionary<int, ToastGlyph> glyphs)
    {
        LineHeight = lineHeight;
        _glyphs = glyphs;
    }

    /// <summary>The characters drawn for names: Latin, Latin-1's letters, Cyrillic, digits and the usual marks.</summary>
    public static IEnumerable<int> Set()
    {
        for (var c = 0x20; c <= 0x7E; c++) yield return c;
        for (var c = 0xC0; c <= 0xFF; c++) yield return c;
        for (var c = 0x410; c <= 0x44F; c++) yield return c;
        foreach (var c in "ЁёІіЇїЄєЎў–—‘’“”«»…·•") yield return c;
    }

    public ToastGlyph? Find(int codepoint) => _glyphs.TryGetValue(codepoint, out var g) ? g : null;

    public IReadOnlyDictionary<int, ToastGlyph> All => _glyphs;

    public string Write()
    {
        var lines = new List<string> { LineHeight.ToString(CultureInfo.InvariantCulture) };
        foreach (var (c, g) in _glyphs.OrderBy(p => p.Key))
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"{c} {g.X} {g.Y} {g.Width} {g.Height} {g.Advance} {g.Left}"));
        return string.Join('\n', lines) + "\n";
    }

    public static ToastGlyphs? Parse(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2 || !int.TryParse(lines[0], CultureInfo.InvariantCulture, out var lineHeight)) return null;
        var glyphs = new Dictionary<int, ToastGlyph>();
        foreach (var line in lines.Skip(1))
        {
            var n = line.Split(' ');
            if (n.Length != 7) continue;
            var v = new int[7];
            var ok = true;
            for (var i = 0; i < 7 && ok; i++) ok = int.TryParse(n[i], CultureInfo.InvariantCulture, out v[i]);
            if (ok) glyphs[v[0]] = new ToastGlyph(v[1], v[2], v[3], v[4], v[5], v[6]);
        }
        return glyphs.Count == 0 ? null : new ToastGlyphs(lineHeight, glyphs);
    }

    /// <summary>
    /// The name laid out from the pen at 0: each character's glyph and its pen x; what does not fit in
    /// <paramref name="room"/> is cut with "…" (a character not in the set is "?").
    /// </summary>
    public IReadOnlyList<(ToastGlyph Glyph, int Pen)> Layout(string name, int room)
    {
        var chars = new List<ToastGlyph>();
        foreach (var rune in name.EnumerateRunes())
            if ((Find(rune.Value) ?? Find('?')) is { } g) chars.Add(g);
        var total = chars.Sum(g => g.Advance);
        if (total > room && Find('…') is { } dots)
        {
            while (chars.Count > 0 && chars.Sum(g => g.Advance) + dots.Advance > room) chars.RemoveAt(chars.Count - 1);
            while (chars.Count > 0 && Find(' ') is { } sp && chars[^1] == sp) chars.RemoveAt(chars.Count - 1);
            chars.Add(dots);
        }
        var placed = new List<(ToastGlyph, int)>(chars.Count);
        var pen = 0;
        foreach (var g in chars)
        {
            placed.Add((g, pen));
            pen += g.Advance;
        }
        return placed;
    }
}
