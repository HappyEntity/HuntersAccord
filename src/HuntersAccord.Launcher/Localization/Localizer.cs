using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using HuntersAccord.Core;

namespace HuntersAccord.Launcher.Localization;

public sealed record LanguageOption(string Code, string NativeName, string? Author, string Path);

/// <summary>
/// Runtime-switchable UI strings from flat <c>lang/&lt;code&gt;.json</c> files ("$language" is the native name, "$author" optional credit).
/// Files in the data folder's <c>lang</c> directory override those next to the launcher; embedded English fills missing keys.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    public const string DefaultLanguage = "en";

    public static Localizer Instance { get; } = new();

    private readonly Dictionary<string, string> _fallback;
    private Dictionary<string, string> _table;

    private Localizer()
    {
        _fallback = LoadEmbeddedEnglish();
        _table = _fallback;
        Languages = DiscoverLanguages();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public IReadOnlyList<LanguageOption> Languages { get; }

    public string Language { get; private set; } = DefaultLanguage;

    public static IEnumerable<string> SearchDirectories =>
    [
        Path.Combine(AppContext.BaseDirectory, "lang"),
        Path.Combine(AppPaths.DataRoot, "lang"),
    ];

    public string this[string key] =>
        _table.TryGetValue(key, out var value) || _fallback.TryGetValue(key, out value) ? value : $"#{key}";

    public bool Has(string key) => _table.ContainsKey(key) || _fallback.ContainsKey(key);

    public string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, this[key], args);

    private readonly Dictionary<string, Dictionary<string, string>> _otherTables = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A string in a specific language (not the current UI one), falling back to English.</summary>
    public string Get(string languageCode, string key)
    {
        var table = TableOf(languageCode);
        return table.TryGetValue(key, out var value) || _fallback.TryGetValue(key, out value) ? value : key;
    }

    /// <summary>Every language's table (English as <see cref="DefaultLanguage"/>); a missing key falls back to English.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> AllTables() =>
        Languages.ToDictionary(l => l.Code, l => (IReadOnlyDictionary<string, string>)TableOf(l.Code), StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, string> TableOf(string languageCode)
    {
        if (!_otherTables.TryGetValue(languageCode, out var table))
        {
            var option = Languages.FirstOrDefault(l => l.Code.Equals(languageCode, StringComparison.OrdinalIgnoreCase));
            table = _fallback;
            if (option is not null && option.Code != DefaultLanguage)
            {
                try { table = ReadTable(File.ReadAllText(option.Path)); }
                catch (Exception e) when (e is IOException or JsonException) { table = _fallback; }
            }
            _otherTables[languageCode] = table;
        }
        return table;
    }

    public void SetLanguage(string code)
    {
        var option = Languages.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        Dictionary<string, string>? table = null;
        if (option is not null && option.Code != DefaultLanguage)
        {
            try { table = ReadTable(File.ReadAllText(option.Path)); }
            catch (Exception e) when (e is IOException or JsonException) { table = null; }
        }

        _table = table ?? _fallback;
        Language = table is null ? DefaultLanguage : option!.Code;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static List<LanguageOption> DiscoverLanguages()
    {
        var found = new Dictionary<string, LanguageOption>(StringComparer.OrdinalIgnoreCase)
        {
            [DefaultLanguage] = new(DefaultLanguage, "English", null, ""),
        };

        foreach (var dir in SearchDirectories.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
            {
                var code = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (code == DefaultLanguage) continue; // English always comes from the embedded copy.
                try
                {
                    var table = ReadTable(File.ReadAllText(file));
                    found[code] = new LanguageOption(code,
                        table.GetValueOrDefault("$language") ?? code,
                        table.GetValueOrDefault("$author"),
                        file);
                }
                catch (Exception e) when (e is IOException or JsonException)
                {
                    // A broken community file must not break the launcher; it is simply not listed.
                }
            }
        }

        return found.Values.OrderBy(l => l.Code == DefaultLanguage ? "" : l.NativeName).ToList();
    }

    private static Dictionary<string, string> LoadEmbeddedEnglish()
    {
        using var stream = typeof(Localizer).Assembly.GetManifestResourceStream("HuntersAccord.Launcher.lang.en.json")
                           ?? throw new InvalidOperationException("Embedded English strings are missing.");
        using var reader = new StreamReader(stream);
        return ReadTable(reader.ReadToEnd());
    }

    private static Dictionary<string, string> ReadTable(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
                table[prop.Name] = prop.Value.GetString()!;
        }
        return table;
    }
}
