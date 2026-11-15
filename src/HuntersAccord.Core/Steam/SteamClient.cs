using System.Diagnostics;

namespace HuntersAccord.Core.Steam;

public enum SteamClientStatus
{
    NotFound,

    NotRunning,

    SignedOut,

    /// <summary>Signed in, in Steam's offline mode: no lobbies, no messages.</summary>
    Offline,

    Ready,
}

public sealed record SteamClientState(SteamClientStatus Status, ulong SteamId, string? PersonaName)
{
    public string? GameName => PersonaName is null || SteamId == 0 ? null : SteamNames.Make(PersonaName, SteamId);

    /// <summary>Reads the client's state: its process, the account signed in (from the OS; null when it cannot tell) and that account's name and mode from loginusers.vdf.</summary>
    public static SteamClientState Read(string? steamRoot, uint? activeAccount)
    {
        if (steamRoot is null) return new SteamClientState(SteamClientStatus.NotFound, 0, null);
        if (!IsRunning()) return new SteamClientState(SteamClientStatus.NotRunning, 0, null);
        if (activeAccount == 0) return new SteamClientState(SteamClientStatus.SignedOut, 0, null);

        // loginusers.vdf: "users" { "<SteamID64>" { "PersonaName" … "MostRecent" "1" "WantsOfflineMode" "0" } }
        var users = ReadUsers(steamRoot);
        const ulong individual = 76561197960265728;
        var user = activeAccount is { } account
            ? users.FirstOrDefault(u => u.Id == individual + account)
            : users.FirstOrDefault(u => u.MostRecent);
        if (user.Id == 0)
            return new SteamClientState(SteamClientStatus.Ready, activeAccount is { } id ? individual + id : 0, null);
        return new SteamClientState(user.Offline ? SteamClientStatus.Offline : SteamClientStatus.Ready, user.Id, user.Name);
    }

    private static bool IsRunning()
    {
        var processes = Process.GetProcessesByName("steam");
        foreach (var process in processes) process.Dispose();
        return processes.Length > 0;
    }

    private static List<(ulong Id, string? Name, bool MostRecent, bool Offline)> ReadUsers(string steamRoot)
    {
        var list = new List<(ulong, string?, bool, bool)>();
        try
        {
            var path = Path.Combine(steamRoot, "config", "loginusers.vdf");
            if (!File.Exists(path)) return list;
            var users = VdfReader.Parse(File.ReadAllText(path)).Child("users");
            if (users is null) return list;
            foreach (var (key, node) in users.Children)
            {
                if (!ulong.TryParse(key, out var id)) continue;
                list.Add((id, node.Value("PersonaName"), node.Value("MostRecent") == "1", node.Value("WantsOfflineMode") == "1"));
            }
        }
        catch (IOException)
        {
            // Steam writing it right now: read again at the next check.
        }
        return list;
    }
}
