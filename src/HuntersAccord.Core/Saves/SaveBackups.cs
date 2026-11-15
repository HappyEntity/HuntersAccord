using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HuntersAccord.Core.Emulator;

namespace HuntersAccord.Core.Saves;

/// <summary>Copies of a player's saved games, one folder per copy: save-backups/player&lt;N&gt;/&lt;yyyyMMdd-HHmmss&gt;[-label]/ with the savedata tree and info.json.</summary>
public static class SaveBackups
{
    public static string Root => Path.Combine(AppPaths.DataRoot, "save-backups");

    private static string PlayerDir(int player) => Path.Combine(Root, $"player{player}");

    private const string InfoFile = "info.json";

    /// <summary>A copy: its id (folder name), when it was made, its label and where the player stood.</summary>
    public sealed record Backup(string Id, DateTime Time, string? Label, string? Map, string? Place, float[]? Position)
    {
        public override string ToString() =>
            $"{Id}  {Time:yyyy-MM-dd HH:mm:ss}{(Label is null ? "" : $"  «{Label}»")}{(Place is null ? "" : $"  {Place}")}" +
            (Map is null ? "" : $"  map {Map}") + (Position is null ? "" : $"  at {string.Join(", ", Position.Select(p => p.ToString("0.0", CultureInfo.InvariantCulture)))}");
    }

    internal sealed record Info(DateTime Time, string? Label, string? Map, string? Place, float[]? Position);

    /// <summary>Copies the player's saves now; the copy, or null when there is nothing to copy.</summary>
    public static Backup? Make(int player, string? label = null, string? map = null, string? place = null, float[]? position = null, int? keep = null)
    {
        var home = EmulatorProfile.ForPlayer(player).HomeDir;
        var time = DateTime.Now;
        var id = time.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + (label is null ? "" : "-" + Slug(label));
        var dir = Path.Combine(PlayerDir(player), id);
        if (CopySaves(home, dir) == 0)
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            return null;
        }
        var info = new Info(time, label, map, place, position);
        File.WriteAllText(Path.Combine(dir, InfoFile), JsonSerializer.Serialize(info, BackupJson.Default.Info));
        // At most so many copies (the settings' SaveBackupsKept): the oldest go.
        foreach (var old in List(player).Skip(Math.Max(1, keep ?? Settings.GameSettings.Load().SaveBackupsKept))) Directory.Delete(Path.Combine(PlayerDir(player), old.Id), recursive: true);
        return new Backup(id, time, label, map, place, position);
    }

    public static IReadOnlyList<Backup> List(int player)
    {
        var dir = PlayerDir(player);
        if (!Directory.Exists(dir)) return [];
        return [.. Directory.EnumerateDirectories(dir).Select(Read).OfType<Backup>().OrderByDescending(b => b.Time).ThenByDescending(b => b.Id, StringComparer.Ordinal)];
    }

    /// <summary>The player's copies kept in another data folder, newest first; their ids are full paths.</summary>
    public static IReadOnlyList<Backup> ListIn(string dataRoot, int player)
    {
        var dir = Path.Combine(dataRoot, "save-backups", $"player{player}");
        if (!Directory.Exists(dir)) return [];
        return [.. Directory.EnumerateDirectories(dir).Select(d => Read(Path.GetFullPath(d), Path.GetFullPath(d))).OfType<Backup>()
            .OrderByDescending(b => b.Time).ThenByDescending(b => b.Id, StringComparer.Ordinal)];
    }

    /// <summary>The folder of a copy: its own id when that is a full path (a copy kept elsewhere), else in the player's copies.</summary>
    public static string DirOf(int player, Backup backup) =>
        Path.IsPathRooted(backup.Id) ? backup.Id : Path.Combine(PlayerDir(player), backup.Id);

    private static Backup? Read(string dir) => Read(dir, Path.GetFileName(dir));

    private static Backup? Read(string dir, string id)
    {
        try
        {
            var info = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(dir, InfoFile)), BackupJson.Default.Info);
            return info is null ? null : new Backup(id, info.Time, info.Label, info.Map, info.Place, info.Position);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Backup(id, Directory.GetCreationTime(dir), null, null, null, null);
        }
    }

    /// <summary>The copy meant by "last", a number (1 = newest) or an id (or its start); a folder kept elsewhere also works. Null when there is none.</summary>
    public static Backup? Find(int player, string which)
    {
        // A copy kept elsewhere (a folder with player1/player2, or the copy's own folder): its full path is its id.
        foreach (var dir in new[] { Path.Combine(which, $"player{player}"), which })
            if (which.Length > 0 && File.Exists(Path.Combine(dir, InfoFile)))
                return Read(Path.GetFullPath(dir), Path.GetFullPath(dir));
        var all = List(player);
        if (which is "last" or "") return all.FirstOrDefault();
        if (int.TryParse(which, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= all.Count && which.Length < 6)
            return all[n - 1];
        return all.FirstOrDefault(b => b.Id == which) ?? all.FirstOrDefault(b => b.Id.StartsWith(which, StringComparison.Ordinal));
    }

    /// <summary>Puts a copy back into the player's profile (the game must be closed). The current saves are copied first ("before-restore", unless keepCurrent is false), so a restore can be undone.</summary>
    public static Backup? Restore(int player, Backup backup, bool keepCurrent = true)
    {
        var home = EmulatorProfile.ForPlayer(player).HomeDir;
        var source = DirOf(player, backup);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException($"No backup {backup.Id} for player {player}.");
        // Kept beyond the limit for now: the oldest copy may be the one being put back.
        var before = keepCurrent ? Make(player, "before-restore", keep: List(player).Count + 1) : null;
        foreach (var user in Directory.EnumerateDirectories(source))
        {
            var target = Path.Combine(home, Path.GetFileName(user), "savedata");
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            CopyTree(Path.Combine(user, "savedata"), target);
        }
        return before;
    }

    public static void Delete(int player, Backup backup)
    {
        var dir = Path.Combine(PlayerDir(player), backup.Id);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    /// <summary>The newest write to the player's save files (to wait for the game's save before copying).</summary>
    public static DateTime LastSaveWrite(int player)
    {
        var home = EmulatorProfile.ForPlayer(player).HomeDir;
        if (!Directory.Exists(home)) return DateTime.MinValue;
        return Directory.EnumerateFiles(home, "*", SearchOption.AllDirectories)
            .Where(f => f.Contains($"{Path.DirectorySeparatorChar}savedata{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.GetLastWriteTime).DefaultIfEmpty(DateTime.MinValue).Max();
    }

    private static string Slug(string label) =>
        new([.. label.Trim().Select(c => char.IsLetterOrDigit(c) ? c : '-').Take(40)]);

    /// <summary>Each user's non-empty savedata under home into to/&lt;user&gt;/savedata; the files copied.</summary>
    private static int CopySaves(string home, string to)
    {
        var files = 0;
        if (!Directory.Exists(home)) return 0;
        foreach (var user in Directory.EnumerateDirectories(home))
        {
            var saves = Path.Combine(user, "savedata");
            if (Directory.Exists(saves) && Directory.EnumerateFileSystemEntries(saves).Any())
                files += CopyTree(saves, Path.Combine(to, Path.GetFileName(user), "savedata"));
        }
        return files;
    }

    private static int CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        var files = 0;
        foreach (var file in Directory.EnumerateFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)), overwrite: true);
            files++;
        }
        foreach (var dir in Directory.EnumerateDirectories(from))
            files += CopyTree(dir, Path.Combine(to, Path.GetFileName(dir)));
        return files;
    }
}

[JsonSerializable(typeof(SaveBackups.Info))]
internal sealed partial class BackupJson : JsonSerializerContext;
