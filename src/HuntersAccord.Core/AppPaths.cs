namespace HuntersAccord.Core;

/// <summary>Per-OS locations of Hunters' Accord's own data (never inside the shadPS4 or game folders).</summary>
public static class AppPaths
{
    private const string FolderName = "HuntersAccord";

    /// <summary>Environment variable that moves the whole data folder elsewhere (a full path); programs started with it inherit it.</summary>
    public const string DataRootVariable = "HA_DATA_ROOT";

    public static string LauncherDataRoot => Path.Combine(DataParent(), FolderName);

    /// <summary>The data folder: <see cref="DataRootVariable"/> when set, else <see cref="LauncherDataRoot"/>. Read on each call, not cached.</summary>
    public static string DataRoot => DataRootFor(Environment.GetEnvironmentVariable(DataRootVariable));

    public static bool IsDataRootMoved => !SamePath(DataRoot, LauncherDataRoot);

    internal static string DataRootFor(string? variable) =>
        string.IsNullOrWhiteSpace(variable) ? LauncherDataRoot : Path.GetFullPath(variable.Trim());

    public static string EmulatorsDir => Path.Combine(DataRoot, "shadps4");

    public static string SettingsFile => Path.Combine(DataRoot, "settings.json");

    public static string LogsDir => Path.Combine(DataRoot, "logs");

    public static string SpotsCapturedFile => Path.Combine(DataRoot, "spots-captured.jsonl");

    public static string SnapshotsDir => Path.Combine(DataRoot, "snapshots");

    /// <summary>Whether <paramref name="path"/> is <paramref name="folder"/> or inside it (full paths compared, no I/O).</summary>
    public static bool IsInside(string path, string folder)
    {
        var p = Normalized(path);
        var f = Normalized(folder);
        return p.Equals(f, PathComparison) || p.StartsWith(f + Path.DirectorySeparatorChar, PathComparison);
    }

    /// <summary>Whether two paths name the same folder (full paths compared, no I/O).</summary>
    public static bool SamePath(string a, string b) => Normalized(a).Equals(Normalized(b), PathComparison);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string Normalized(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));

    private static string DataParent()
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
            return Path.Combine(home, "Library", "Application Support");

        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return string.IsNullOrEmpty(xdg) ? Path.Combine(home, ".local", "share") : xdg;
    }
}
