using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using HuntersAccord.Core.Trophies;

namespace HuntersAccord.Core.Pkg;

/// <summary>What an import of a keys.json took from it.</summary>
public enum ImportedTrophyKey
{
    /// <summary>The file has no trophy key (or an empty one).</summary>
    None,

    /// <summary>Checked on the game copy and stored.</summary>
    Valid,

    /// <summary>No game copy to check it on, and none stored yet: stored, checked once a copy is there.</summary>
    Unverified,

    /// <summary>Not stored: it does not open the game copy's trophies, or is not 32 hex characters.</summary>
    Wrong,

    /// <summary>Not stored: no game copy to check it on, and a key is stored already (it is kept).</summary>
    KeptStored,
}

/// <summary>The outcome of <see cref="KeyFile.Import"/>.</summary>
public sealed record KeyImport(bool PkgKeys, ImportedTrophyKey TrophyKey)
{
    public bool Changed => PkgKeys || TrophyKey is ImportedTrophyKey.Valid or ImportedTrophyKey.Unverified;
}

/// <summary>
/// The launcher's own keys.json (shadPS4's key manager format): read, and changed only by merging, never copied over, so
/// a good section is never lost to an empty or wrong one. The sections: <c>FakeKeyset</c> and <c>PkgDerivedKey3Keyset</c>
/// (unpacking PKG files, taken only complete), <c>DebugRifKeyset</c> (kept, taken when complete) and
/// <c>TrophyKeySet.ReleaseTrophyKey</c> (taken only when it opens the game copy's trophies, see <see cref="StoreTrophyKey"/>).
/// Other sections of the file stay as they are. Values are never logged.
/// </summary>
public static class KeyFile
{
    public const string TrophySection = "TrophyKeySet";
    public const string TrophyField = "ReleaseTrophyKey";

    private static readonly string[] RsaFields = ["Modulus", "PublicExponent", "PrivateExponent", "Prime1", "Prime2", "Exponent1", "Exponent2", "Coefficient"];
    private static readonly string[] PkgSections = ["FakeKeyset", "PkgDerivedKey3Keyset"];
    private const string RifSection = "DebugRifKeyset";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The launcher's own key file.</summary>
    public static string OwnFile => Path.Combine(AppPaths.DataRoot, PkgKeys.FileName);

    /// <summary>The trophy key text of a key file (null: no file, no key, unreadable).</summary>
    public static string? ReadTrophyKey(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return Load(File.ReadAllText(path))?[TrophySection]?[TrophyField] is JsonValue v && v.TryGetValue<string>(out var text)
                   && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Stores <paramref name="key"/> as the trophy key of <paramref name="path"/> when the rule allows: a key that opens the
    /// game copy (<paramref name="check"/> Valid) always; one that cannot be checked (NoGame) only when none is stored yet;
    /// a wrong one never. The rest of the file is kept.
    /// </summary>
    public static ImportedTrophyKey StoreTrophyKey(string path, ReadOnlySpan<byte> key, TrophyKeyCheck check)
    {
        var outcome = Decide(ReadTrophyKey(path) is not null, check);
        if (outcome is ImportedTrophyKey.Valid or ImportedTrophyKey.Unverified)
        {
            var root = LoadFile(path);
            SetTrophyKey(root, TrophyKey.Format(key));
            Save(path, root);
        }
        return outcome;
    }

    /// <summary>The "bad never overwrites good" rule: what happens to a new key given what its check said.</summary>
    public static ImportedTrophyKey Decide(bool hasStored, TrophyKeyCheck check) => check switch
    {
        TrophyKeyCheck.Valid => ImportedTrophyKey.Valid,
        TrophyKeyCheck.NoGame => hasStored ? ImportedTrophyKey.KeptStored : ImportedTrophyKey.Unverified,
        _ => ImportedTrophyKey.Wrong,
    };

    /// <summary>
    /// Merges a user-chosen key file into <paramref name="target"/>: the PKG keysets only when complete, the trophy key by
    /// <see cref="Decide"/> (<paramref name="checkTrophyKey"/> tries it on the game copy). Nothing usable in it (no
    /// complete PKG keys, no trophy key) is an error; so is a file that is not JSON. Existing sections are never dropped.
    /// </summary>
    /// <exception cref="InvalidDataException">The file holds nothing to import.</exception>
    public static KeyImport Import(string sourcePath, string target, Func<byte[], TrophyKeyCheck> checkTrophyKey)
    {
        var source = Load(File.ReadAllText(sourcePath)) ?? throw new InvalidDataException("The file is not a key file (no JSON object).");
        var pkgComplete = PkgSections.All(s => IsComplete(source[s]));
        var trophyText = source[TrophySection]?[TrophyField] is JsonValue v && v.TryGetValue<string>(out var t) && !string.IsNullOrWhiteSpace(t) ? t : null;
        if (!pkgComplete && trophyText is null)
            throw new InvalidDataException("The file holds neither complete FakeKeyset and PkgDerivedKey3Keyset nor a trophy key.");

        var root = LoadFile(target);
        var hasStored = ReadTrophyKey(target) is not null;
        if (pkgComplete)
        {
            foreach (var section in PkgSections) root[section] = source[section]!.DeepClone();
            if (IsComplete(source[RifSection])) root[RifSection] = source[RifSection]!.DeepClone();
        }

        var trophy = ImportedTrophyKey.None;
        if (trophyText is not null)
        {
            trophy = TrophyKey.Parse(trophyText) is { } key ? Decide(hasStored, checkTrophyKey(key)) : ImportedTrophyKey.Wrong;
            if (trophy is ImportedTrophyKey.Valid or ImportedTrophyKey.Unverified) SetTrophyKey(root, TrophyKey.Format(TrophyKey.Parse(trophyText)!));
        }

        var result = new KeyImport(pkgComplete, trophy);
        if (result.Changed) Save(target, root);
        return result;
    }

    private static bool IsComplete(JsonNode? section) =>
        section is JsonObject o && RsaFields.All(f => o[f] is JsonValue v && v.TryGetValue<string>(out var s) && s.Any(Uri.IsHexDigit));

    private static void SetTrophyKey(JsonObject root, string hex)
    {
        if (root[TrophySection] is not JsonObject section)
        {
            section = new JsonObject();
            root[TrophySection] = section;
        }
        section[TrophyField] = hex;
    }

    private static JsonObject? Load(string json) =>
        JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject;

    // The file as it is (a new object when there is none). An unreadable one is not overwritten: the error goes up.
    private static JsonObject LoadFile(string path) =>
        File.Exists(path) ? Load(File.ReadAllText(path)) ?? throw new InvalidDataException($"{path} is not a JSON object.") : new JsonObject();

    private static void Save(string path, JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(Indented) + Environment.NewLine);
        File.Move(temp, path, overwrite: true);
    }
}
