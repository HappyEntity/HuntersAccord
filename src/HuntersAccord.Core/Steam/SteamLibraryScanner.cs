namespace HuntersAccord.Core.Steam;

/// <summary>
/// Reads Steam's library folders to find which FromSoftware games are installed. Only ownership is required to use a game for the session,
/// so this is a UI hint; ownership is verified through the Steam API when the session starts.
/// </summary>
public static class SteamLibraryScanner
{
    public static IReadOnlyList<string> GetLibraryFolders(string steamRoot)
    {
        var folders = new List<string> { steamRoot };
        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) return folders;

        var root = VdfReader.Parse(File.ReadAllText(vdf)).Child("libraryfolders");
        if (root is null) return folders;

        foreach (var entry in root.Children.Values)
        {
            if (entry.Value("path") is { Length: > 0 } path && !folders.Contains(path, StringComparer.OrdinalIgnoreCase))
                folders.Add(path);
        }
        return folders;
    }

    public static IReadOnlyList<FromSoftwareGame> FindInstalledFromSoftwareGames(string steamRoot) =>
        FromSoftwareGames.All
            .Where(g => GetLibraryFolders(steamRoot).Any(lib =>
                File.Exists(Path.Combine(lib, "steamapps", $"appmanifest_{g.AppId}.acf"))))
            .OrderBy(g => g.Priority)
            .ToList();
}
