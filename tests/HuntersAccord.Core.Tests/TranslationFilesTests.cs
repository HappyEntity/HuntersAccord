using System.Text.Json;
using System.Text.RegularExpressions;

namespace HuntersAccord.Core.Tests;

/// <summary>Guards the shipped translations against drifting from the English reference file.</summary>
public partial class TranslationFilesTests
{
    private static string LangDir
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HuntersAccord.slnx"))) dir = dir.Parent;
            return Path.Combine(dir!.FullName, "src", "HuntersAccord.Launcher", "Resources", "Lang");
        }
    }

    public static TheoryData<string> Translations() =>
        new(Directory.EnumerateFiles(LangDir, "*.json").Select(Path.GetFileName).Where(f => f != "en.json")!);

    [Theory]
    [MemberData(nameof(Translations))]
    public void Translation_matches_english_keys_and_placeholders(string file)
    {
        var english = Read("en.json");
        var translated = Read(file);

        Assert.True(translated.ContainsKey("$language"), $"{file}: missing $language");
        var unknown = translated.Keys.Where(k => !k.StartsWith('$') && !english.ContainsKey(k)).ToList();
        Assert.True(unknown.Count == 0, $"{file}: unknown keys {string.Join(", ", unknown)}");

        foreach (var (key, text) in translated.Where(p => !p.Key.StartsWith('$')))
            Assert.Equal(PlaceholderIndices(english[key]), PlaceholderIndices(text));
    }

    private static Dictionary<string, string> Read(string file)
    {
        // The files may have comments (section headings): the launcher skips them too.
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(LangDir, file)),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
    }

    // Compares placeholder indices ({0}, {2:F0}...), translators may change the format part.
    private static string[] PlaceholderIndices(string text) =>
        PlaceholderRegex().Matches(text).Select(m => m.Groups[1].Value).Distinct().Order().ToArray();

    [GeneratedRegex(@"\{(\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex PlaceholderRegex();
}
