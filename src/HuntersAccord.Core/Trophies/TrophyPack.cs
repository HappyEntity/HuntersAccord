using System.Buffers.Binary;
using System.Text;

namespace HuntersAccord.Core.Trophies;

/// <summary>
/// A PS4 game's trophy pack (<c>sce_sys/trophy/trophy00.trp</c>): a plain container. Big-endian header — magic
/// <see cref="Magic"/>, version (u32), file size (u64), entry count (u32), entry size (u32) at 0..24 — and from
/// <see cref="EntriesAt"/> its entries, 64 bytes each: name (32 bytes, NUL-terminated), offset (u64), length (u64). The
/// pictures (<c>TROP000.PNG</c>… one per trophy id, <c>ICON0.PNG</c>) are plain PNG; only the <c>*.ESFM</c> lists are
/// encrypted (not read here). Read from the player's own copy of the game; nothing of it is shipped.
/// </summary>
public sealed class TrophyPack
{
    public const uint Magic = 0xDCA24D00;
    public const int EntriesAt = 0x60;
    public const string RelativePath = "sce_sys/trophy/trophy00.trp";

    private readonly string _path;
    private readonly Dictionary<string, (long Offset, long Length)> _entries;

    private TrophyPack(string path, Dictionary<string, (long, long)> entries)
    {
        _path = path;
        _entries = entries;
    }

    public string Path => _path;

    public IReadOnlyCollection<string> Names => _entries.Keys;

    /// <summary>The picture of a trophy (its id as the game numbers it): <c>TROP&lt;id:000&gt;.PNG</c>.</summary>
    public static string PictureName(int id) => $"TROP{id:000}.PNG";

    /// <summary>The pack of a game copy: the update's first (it may replace the base's), then the base's; null when neither has one.</summary>
    public static string? Find(string? basePath, string? updatePath)
    {
        foreach (var folder in new[] { updatePath, basePath })
        {
            if (string.IsNullOrEmpty(folder)) continue;
            var file = System.IO.Path.Combine(folder, "sce_sys", "trophy", "trophy00.trp");
            if (File.Exists(file)) return file;
        }
        return null;
    }

    /// <summary>The pack's table read; null when the file is not a trophy pack (or is cut short).</summary>
    public static TrophyPack? Open(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var header = new byte[24];
            if (stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length) return null;
            if (BinaryPrimitives.ReadUInt32BigEndian(header) != Magic) return null;
            var count = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16));
            var size = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20));
            if (count is 0 or > 4096 || size < 48 || size > 1024) return null;
            var table = new byte[count * size];
            stream.Position = EntriesAt;
            if (stream.ReadAtLeast(table, table.Length, throwOnEndOfStream: false) < table.Length) return null;
            var entries = new Dictionary<string, (long, long)>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < count; i++)
            {
                var entry = table.AsSpan((int)(i * size), (int)size);
                var nameBytes = entry[..32];
                var end = nameBytes.IndexOf((byte)0);
                var name = Encoding.ASCII.GetString(end < 0 ? nameBytes : nameBytes[..end]);
                var offset = (long)BinaryPrimitives.ReadUInt64BigEndian(entry[32..]);
                var length = (long)BinaryPrimitives.ReadUInt64BigEndian(entry[40..]);
                if (name.Length == 0 || offset < 0 || length <= 0 || offset + length > stream.Length) continue;
                entries.TryAdd(name, (offset, length));
            }
            return new TrophyPack(path, entries);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>An entry's bytes; null when the pack has none of that name.</summary>
    public byte[]? Read(string name)
    {
        if (!_entries.TryGetValue(name, out var at) || at.Length > 64 * 1024 * 1024) return null;
        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Position = at.Offset;
        var bytes = new byte[at.Length];
        return stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false) == bytes.Length ? bytes : null;
    }

    /// <summary>An entry that is a PNG (starts with the PNG signature); null otherwise.</summary>
    public byte[]? ReadPng(string name) => Read(name) is { Length: > 8 } bytes && bytes.AsSpan(0, 8).SequenceEqual(PngSignature) ? bytes : null;

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
}

/// <summary>
/// The trophies' pictures taken out of the player's own trophy pack into the data folder (<see cref="FolderName"/>), once
/// per pack (a stamp: its path, size and time). Never in the repository or a release: read from the game copy at run time.
/// </summary>
public static class TrophyPictures
{
    public const string FolderName = "trophy-pictures";
    private const string StampName = "source.txt";

    public static string FolderIn(string dataRoot) => System.IO.Path.Combine(dataRoot, FolderName);

    /// <summary>A trophy's picture file in the cache (it may not be there).</summary>
    public static string FileIn(string dataRoot, int id) => System.IO.Path.Combine(FolderIn(dataRoot), TrophyPack.PictureName(id));

    /// <summary>
    /// The pictures of <paramref name="packPath"/> in the cache (taken out unless the stamp says they are already); the
    /// folder (null without a pack, or when it is not a trophy pack). The cache follows the last pack asked for.
    /// </summary>
    public static string? Ensure(string dataRoot, string? packPath)
    {
        if (packPath is null) return null;
        var folder = FolderIn(dataRoot);
        try
        {
            var info = new FileInfo(packPath);
            if (!info.Exists) return null;
            var stamp = $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
            var stampFile = System.IO.Path.Combine(folder, StampName);
            if (File.Exists(stampFile) && File.ReadAllText(stampFile).Trim() == stamp) return folder;
            if (TrophyPack.Open(packPath) is not { } pack) return null;
            Directory.CreateDirectory(folder);
            foreach (var old in Directory.EnumerateFiles(folder, "*.PNG")) File.Delete(old);
            foreach (var name in pack.Names)
            {
                if (!name.EndsWith(".PNG", StringComparison.OrdinalIgnoreCase)) continue;
                if (!name.StartsWith("TROP", StringComparison.OrdinalIgnoreCase) && !name.Equals("ICON0.PNG", StringComparison.OrdinalIgnoreCase)) continue;
                if (pack.ReadPng(name) is not { } png) continue;
                var target = System.IO.Path.Combine(folder, name.ToUpperInvariant());
                File.WriteAllBytes(target + ".tmp", png);
                File.Move(target + ".tmp", target, overwrite: true);
            }
            File.WriteAllText(stampFile, stamp);
            return folder;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Directory.Exists(folder) ? folder : null;
        }
    }

    /// <summary>The cached picture of a trophy, when there is one.</summary>
    public static string? Find(string dataRoot, int id)
    {
        var file = FileIn(dataRoot, id);
        return File.Exists(file) ? file : null;
    }
}
