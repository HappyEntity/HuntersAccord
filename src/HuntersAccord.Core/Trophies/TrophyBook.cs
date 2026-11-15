using System.Text.Json;
using System.Text.Json.Serialization;
using HuntersAccord.Core.Emulator;

namespace HuntersAccord.Core.Trophies;

/// <summary>How a trophy came to be held.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TrophyHow>))]
public enum TrophyHow
{
    /// <summary>The game awarded it (its own award, as on a PS4).</summary>
    Game,
    /// <summary>The co-op mod credited it: what the game gives only the world's owner, given to a guest that took part.</summary>
    Coop,
    /// <summary>Found already earned in the player's save (the first start with trophies): no date.</summary>
    Save,
    /// <summary>The platinum, once every trophy it needs was held.</summary>
    Platinum,
}

/// <summary>
/// A trophy held: when (null when found in the save), with whom (the session's other players: their characters' names,
/// an online name for one whose character's name was not known yet; <see cref="PartnerId"/> their online names), how.
/// Entries from before the characters' names have the online names as <see cref="Partner"/> and no <see cref="PartnerId"/>.
/// </summary>
public sealed record TrophyEntry(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("earned")] DateTimeOffset? Earned,
    [property: JsonPropertyName("partner")] string? Partner,
    [property: JsonPropertyName("how")] TrophyHow How,
    [property: JsonPropertyName("partnerId")] string? PartnerId = null)
{
    /// <summary>Counted from the save, without a date ("credited from the save").</summary>
    [JsonIgnore]
    public bool FromSave => Earned is null;
}

/// <summary>
/// A player's own trophies: <c>trophies.json</c> in its profile folder (<see cref="EmulatorProfile.RootIn"/>), written by
/// the mod, read by the launcher. Each trophy once (the first award kept); the platinum added when the base game's other
/// trophies are all held. The file is replaced whole (written next to it, then moved), so a reader never sees half of it.
/// <code>
/// { "version": 1, "trophies": [ { "id": 14, "earned": "2026-10-26T10:00:00+00:00", "partner": "Nika", "how": "Coop", "partnerId": "Hunter-td" } ] }
/// </code>
/// </summary>
public sealed class TrophyBook
{
    public const string FileName = "trophies.json";
    public const int Version = 1;

    /// <summary>The prerendered in-game notices' folder in the data folder (<see cref="TrophyToastFiles"/>, the launcher's language).</summary>
    public const string ToastFolderName = "trophy-toasts";

    /// <summary>The height the overlay's own built-in notice is drawn for (a 2160-line frame; scaled by the frame's height).</summary>
    public const int ToastReferenceHeight = 2160;

    private readonly Dictionary<int, TrophyEntry> _held = [];

    public static string FileIn(string dataRoot, int player) => Path.Combine(EmulatorProfile.RootIn(dataRoot, player), FileName);

    public static string FileFor(int player) => FileIn(AppPaths.DataRoot, player);

    public static string ToastFolderIn(string dataRoot) => Path.Combine(dataRoot, ToastFolderName);

    public IReadOnlyCollection<TrophyEntry> Held => _held.Values;

    public bool Has(int id) => _held.ContainsKey(id);

    public TrophyEntry? Get(int id) => _held.GetValueOrDefault(id);

    /// <summary>
    /// A trophy earned: added once (an id not in <see cref="TrophyCatalog"/> is not), and the platinum after it when the
    /// base game is complete. Returns the trophies new in the book, in order (empty: held before). The partner: the
    /// characters' names shown; <paramref name="partnerId"/>: the same players' online names (kept without a partner only).
    /// </summary>
    public IReadOnlyList<TrophyEntry> Award(int id, DateTimeOffset? earned, string? partner, TrophyHow how, string? partnerId = null)
    {
        if (TrophyCatalog.Find(id) is null || _held.ContainsKey(id)) return [];
        var added = new List<TrophyEntry>(2);
        var shown = string.IsNullOrWhiteSpace(partner) ? null : partner.Trim();
        var ids = shown is null || string.IsNullOrWhiteSpace(partnerId) ? null : partnerId.Trim();
        var entry = new TrophyEntry(id, how == TrophyHow.Save ? null : earned, shown, how, ids);
        _held[id] = entry;
        added.Add(entry);
        if (PlatinumDue())
        {
            var platinum = new TrophyEntry(TrophyCatalog.PlatinumId, entry.Earned, entry.Partner, entry.FromSave ? TrophyHow.Save : TrophyHow.Platinum, entry.PartnerId);
            _held[platinum.Id] = platinum;
            added.Add(platinum);
        }
        return added;
    }

    /// <summary>The platinum not held yet, every trophy it needs held.</summary>
    public bool PlatinumDue() => !_held.ContainsKey(TrophyCatalog.PlatinumId) && TrophyCatalog.PlatinumNeeds.All(_held.ContainsKey);

    /// <summary>Removed (a test's preparation); the platinum stays as it was.</summary>
    public bool Forget(int id) => _held.Remove(id);

    /// <summary>The book in a file; an empty one when there is none. A file that cannot be read is kept aside (.bad) and an empty book given.</summary>
    public static TrophyBook Load(string path)
    {
        var book = new TrophyBook();
        if (!File.Exists(path)) return book;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var file = JsonSerializer.Deserialize(stream, TrophyJson.Default.TrophyFile);
            foreach (var entry in file?.Trophies ?? [])
                if (TrophyCatalog.Find(entry.Id) is not null) book._held.TryAdd(entry.Id, entry);
        }
        catch (JsonException)
        {
            TryKeepAside(path);
        }
        return book;
    }

    /// <summary>Read only: a book that cannot be read is empty, the file left as it is (the launcher's view).</summary>
    public static TrophyBook Read(string path)
    {
        var book = new TrophyBook();
        try
        {
            if (!File.Exists(path)) return book;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            foreach (var entry in JsonSerializer.Deserialize(stream, TrophyJson.Default.TrophyFile)?.Trophies ?? [])
                if (TrophyCatalog.Find(entry.Id) is not null) book._held.TryAdd(entry.Id, entry);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // Being replaced, or not ours to read: nothing shown this time.
        }
        return book;
    }

    /// <summary>
    /// Written whole: into a file next to it, then moved over it. The move is refused (UnauthorizedAccessException, an
    /// IOException) while another program holds the old file open without delete sharing (a reader, an indexer, a virus
    /// scan): tried again for about <see cref="ReplaceTries"/> × <see cref="ReplaceWait"/>, each time also by File.Replace;
    /// still refused, the old file is copied aside (.bak) and the new one copied over it. Whatever fails, the old file is
    /// either whole where it was or in the .bak; the last refusal is thrown.
    /// </summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var file = new TrophyFile(Version, [.. _held.Values.OrderBy(e => e.Id)]);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(file, TrophyJson.Default.TrophyFile));
        PutInPlace(temp, path);
    }

    private const int ReplaceTries = 10;
    private static readonly TimeSpan ReplaceWait = TimeSpan.FromMilliseconds(100);

    private static void PutInPlace(string temp, string path)
    {
        Exception? last = null;
        for (var i = 0; i < ReplaceTries; i++)
        {
            if (i > 0) Thread.Sleep(ReplaceWait);
            try
            {
                File.Move(temp, path, overwrite: true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                last = e;
            }
            if (!File.Exists(path)) continue; // File.Replace needs the old file
            try
            {
                File.Replace(temp, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                last = e;
            }
        }
        // Last: a copy over it, the old one kept aside first (a copy that fails half-way would leave a cut file).
        try
        {
            if (File.Exists(path)) File.Copy(path, path + ".bak", overwrite: true);
            File.Copy(temp, path, overwrite: true);
            File.Delete(temp);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw last ?? e;
        }
    }

    private static void TryKeepAside(string path)
    {
        try
        {
            File.Copy(path, path + ".bad", overwrite: true);
        }
        catch (IOException)
        {
            // Not worth failing over.
        }
    }
}

/// <summary>The trophies file as written.</summary>
public sealed record TrophyFile(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("trophies")] List<TrophyEntry> Trophies);

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TrophyFile))]
internal sealed partial class TrophyJson : JsonSerializerContext;
