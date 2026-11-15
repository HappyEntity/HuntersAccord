using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace HuntersAccord.Core.Trophies;

/// <summary>A trophy as the game's own list gives it: its name and detail in one language, its grade and hidden flag.</summary>
public sealed record OfficialTrophy(int Id, string Name, string Detail, TrophyGrade Grade, bool Hidden);

/// <summary>
/// The game's own trophy texts, decrypted at run time from the player's game copy with the player's trophy key
/// (<see cref="TrophyKey"/>). Kept in memory only: never written, logged, shipped or sent. The lists: <c>TROPCONF.ESFM</c>
/// (ids, grades, hidden flags), <c>TROP.ESFM</c> (the default language) and <c>TROP_xx.ESFM</c> per PS4 system language
/// (<see cref="ListName"/>).
/// </summary>
public static class OfficialTrophies
{
    public const string DefaultList = "TROP.ESFM";

    /// <summary>
    /// The list of a PS4 system language: <c>TROP_&lt;index:00&gt;.ESFM</c> for the 21 languages (0 ja, 1 en-US, 2 fr, 3 es, 4 de,
    /// 5 it, 6 nl, 7 pt-PT, 8 ru, 9 ko, 10 zh-Hant, 11 zh-Hans, 12 fi, 13 sv, 14 da, 15 no, 16 pl, 17 pt-BR, 18 en-GB, 19 tr,
    /// 20 es-419; checked against a real pack's scripts), the default list for any other.
    /// </summary>
    public static string ListName(int language) => language is >= 0 and <= 20 ? $"TROP_{language:00}.ESFM" : DefaultList;

    /// <summary>
    /// The texts of <paramref name="pack"/> in <paramref name="language"/> (its list, else the default, else US English);
    /// null when the key does not open the pack or nothing in it reads.
    /// </summary>
    public static IReadOnlyDictionary<int, OfficialTrophy>? Read(TrophyPack pack, ReadOnlySpan<byte> trophyKey, string npCommId, int language)
    {
        if (trophyKey.Length != TrophyKey.Size) return null;
        var listKey = TrophyKey.DeriveListKey(trophyKey, npCommId);
        string? Open(string name) =>
            pack.Read(name) is { } entry && TrophyKey.Decrypt(entry, listKey) is { } plain && TrophyKey.LooksLikeList(plain)
                ? Encoding.UTF8.GetString(plain) : null;

        if (Open(TrophyKey.ConfigName) is not { } config) return null;
        var flags = Parse(config);
        foreach (var name in new[] { ListName(language), DefaultList, ListName(1) }.Distinct())
        {
            if (Open(name) is not { } list) continue;
            var texts = Parse(list);
            if (texts.Count == 0) continue;
            return texts.Values.ToDictionary(t => t.Id, t => flags.TryGetValue(t.Id, out var f) ? t with { Grade = f.Grade, Hidden = f.Hidden } : t);
        }
        return null;
    }

    /// <summary>
    /// The trophies of one list's XML (after the signature comment, which precedes the XML declaration and so is cut off
    /// first); names and details empty in the configuration. Unreadable XML: none.
    /// </summary>
    public static Dictionary<int, OfficialTrophy> Parse(string xml)
    {
        var result = new Dictionary<int, OfficialTrophy>();
        var start = xml.IndexOf("<trophyconf", StringComparison.Ordinal);
        if (start < 0) return result;
        // The block cipher's padding follows the list: zeros in some languages' lists, PKCS#7 bytes (0x01..0x10) in others.
        const string close = "</trophyconf>";
        var end = xml.LastIndexOf(close, StringComparison.Ordinal);
        if (end < start) return result;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(xml[start..(end + close.Length)]), settings);
            var root = XDocument.Load(reader).Root;
            if (root is null) return result;
            foreach (var trophy in root.Descendants("trophy"))
            {
                if (!int.TryParse((string?)trophy.Attribute("id"), out var id)) continue;
                var grade = ((string?)trophy.Attribute("ttype"))?.Trim().ToUpperInvariant() switch
                {
                    "P" => TrophyGrade.Platinum,
                    "G" => TrophyGrade.Gold,
                    "S" => TrophyGrade.Silver,
                    _ => TrophyGrade.Bronze,
                };
                var hidden = string.Equals((string?)trophy.Attribute("hidden"), "yes", StringComparison.OrdinalIgnoreCase);
                var name = ((string?)trophy.Element("name"))?.Trim() ?? "";
                var detail = ((string?)trophy.Element("detail"))?.Trim() ?? "";
                result.TryAdd(id, new OfficialTrophy(id, name, detail, grade, hidden));
            }
        }
        catch (XmlException)
        {
            result.Clear();
        }
        return result;
    }

    /// <summary>
    /// Our list's entry with the game's own words in place of ours (the same title and detail in every language slot),
    /// its grade and hidden flag; unchanged when the game's list has no such trophy or no name for it.
    /// </summary>
    public static TrophyInfo Apply(TrophyInfo info, IReadOnlyDictionary<int, OfficialTrophy>? texts) =>
        texts is not null && texts.TryGetValue(info.Id, out var t) && t.Name.Length > 0
            ? info with { NameEn = t.Name, NameRu = t.Name, DescriptionEn = t.Detail, DescriptionRu = t.Detail, Grade = t.Grade, Hidden = t.Hidden }
            : info;
}
