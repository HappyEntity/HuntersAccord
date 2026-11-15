using System.Text;

namespace HuntersAccord.Core.Steam;

/// <summary>A player's online name in the game, made from their Steam name (the launcher shows it, the mod uses it).</summary>
public static class SteamNames
{
    /// <summary>
    /// The Steam name as the game takes it (letters, digits, '-', '_'; Cyrillic spelled in Latin), at most 12, and two
    /// characters of the Steam id for two players of the same name: "Hunter-k3".
    /// </summary>
    public static string Make(string persona, ulong id)
    {
        var name = new StringBuilder();
        foreach (var c in persona)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_') name.Append(c);
            else if (Latin.TryGetValue(char.ToLowerInvariant(c), out var latin))
                name.Append(char.IsUpper(c) && latin.Length > 0 ? char.ToUpperInvariant(latin[0]) + latin[1..] : latin);
            else if (c == ' ') name.Append('_');
            if (name.Length >= 12) break;
        }
        var text = name.ToString().Trim('_', '-');
        if (text.Length > 12) text = text[..12];
        if (text.Length < 3) text = "Hunter";
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        var hash = (uint)(id ^ (id >> 32)) * 2654435761u;
        return $"{text}-{digits[(int)(hash % 36)]}{digits[(int)(hash / 36 % 36)]}";
    }

    private static readonly Dictionary<char, string> Latin = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e", ['ж'] = "zh", ['з'] = "z",
        ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r",
        ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh",
        ['щ'] = "sch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya",
    };
}
