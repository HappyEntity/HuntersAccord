using System.Diagnostics;
using System.Text;

namespace HuntersAccord.Core.Steam;

public sealed record SteamApiFile(string Path, string GameName, Version Version);

/// <summary>
/// Steam's library (steam_api64.dll) for online play. Hunters' Accord ships none: it takes one from a game installed in Steam (the
/// newest that has everything the mod calls; games carry different versions, old ones lack the flat API) or the file
/// the player chose.
/// </summary>
public static class SteamApiLibrary
{
    public const string FileName = "steam_api64.dll";

    public static IReadOnlyList<(string Name, string[] Versions)> Accessors { get; } =
    [
        ("SteamUser", ["v023", "v022", "v021", "v020"]),
        ("SteamFriends", ["v018", "v017"]),
        ("SteamMatchmaking", ["v009"]),
        ("SteamNetworkingMessages_SteamAPI", ["v002"]),
        ("SteamNetworkingUtils_SteamAPI", ["v004", "v003"]),
        ("SteamApps", ["v009", "v008"]),
    ];

    public static IReadOnlyList<string> Functions { get; } =
    [
        "SteamAPI_Shutdown", "SteamAPI_GetHSteamPipe", "SteamAPI_ManualDispatch_Init", "SteamAPI_ManualDispatch_RunFrame",
        "SteamAPI_ManualDispatch_GetNextCallback", "SteamAPI_ManualDispatch_FreeLastCallback",
        "SteamAPI_ManualDispatch_GetAPICallResult", "SteamAPI_ISteamUser_GetSteamID", "SteamAPI_ISteamUser_BLoggedOn",
        "SteamAPI_ISteamFriends_GetPersonaName", "SteamAPI_ISteamApps_BIsSubscribedApp",
        "SteamAPI_ISteamMatchmaking_CreateLobby", "SteamAPI_ISteamMatchmaking_SetLobbyData",
        "SteamAPI_ISteamMatchmaking_GetLobbyData", "SteamAPI_ISteamMatchmaking_LeaveLobby",
        "SteamAPI_ISteamMatchmaking_RequestLobbyList", "SteamAPI_ISteamMatchmaking_AddRequestLobbyListStringFilter",
        "SteamAPI_ISteamMatchmaking_AddRequestLobbyListDistanceFilter",
        "SteamAPI_ISteamMatchmaking_AddRequestLobbyListResultCountFilter", "SteamAPI_ISteamMatchmaking_GetLobbyByIndex",
        "SteamAPI_ISteamMatchmaking_GetLobbyOwner", "SteamAPI_ISteamNetworkingMessages_SendMessageToUser",
        "SteamAPI_ISteamNetworkingMessages_ReceiveMessagesOnChannel",
        "SteamAPI_ISteamNetworkingMessages_AcceptSessionWithUser", "SteamAPI_SteamNetworkingMessage_t_Release",
        "SteamAPI_SteamNetworkingIdentity_SetSteamID64", "SteamAPI_SteamNetworkingIdentity_GetSteamID64",
        "SteamAPI_ISteamNetworkingUtils_InitRelayNetworkAccess",
    ];

    public static bool IsSuitable(string path)
    {
        var exports = PeExports.Read(path);
        if (exports.Count == 0) return false;
        if (!exports.Contains("SteamAPI_InitFlat") && !exports.Contains("SteamAPI_Init")) return false;
        return Functions.All(exports.Contains)
               && Accessors.All(a => a.Versions.Any(v => exports.Contains($"SteamAPI_{a.Name}_{v}")));
    }

    public static SteamApiFile? Describe(string path)
    {
        if (!File.Exists(path) || !IsSuitable(path)) return null;
        return new SteamApiFile(path, GameOf(path), VersionOf(path));
    }

    /// <summary>The newest fit library among the games of every Steam library folder; reads files only, so call it off the UI thread.</summary>
    public static SteamApiFile? FindBest(string steamRoot)
    {
        SteamApiFile? best = null;
        var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 4, IgnoreInaccessible = true };
        foreach (var library in SteamLibraryScanner.GetLibraryFolders(steamRoot))
        {
            var common = Path.Combine(library, "steamapps", "common");
            if (!Directory.Exists(common)) continue;
            foreach (var game in SafeDirectories(common))
            {
                List<string> files;
                try
                {
                    files = Directory.EnumerateFiles(game, FileName, options).ToList();
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    continue;
                }
                foreach (var file in files)
                {
                    var version = VersionOf(file);
                    if (best is not null && version <= best.Version) continue;
                    if (IsSuitable(file)) best = new SteamApiFile(file, Path.GetFileName(game), version);
                }
            }
        }
        return best;
    }

    private static string[] SafeDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string GameOf(string path)
    {
        var parts = Path.GetFullPath(path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var common = Array.FindLastIndex(parts, p => p.Equals("common", StringComparison.OrdinalIgnoreCase));
        return common >= 0 && common + 1 < parts.Length - 1
            ? parts[common + 1]
            : Path.GetFileName(Path.GetDirectoryName(path)) ?? "";
    }

    private static Version VersionOf(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new Version(0, 0);
        }
    }
}

/// <summary>The names a Windows DLL exports, read from its file (the PE export table).</summary>
public static class PeExports
{
    public static HashSet<string> Read(string path)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var bytes = File.ReadAllBytes(path);
            var pe = BitConverter.ToInt32(bytes, 0x3C);
            if (BitConverter.ToUInt32(bytes, pe) != 0x4550) return names; // "PE\0\0"
            var sections = BitConverter.ToUInt16(bytes, pe + 6);
            var optionalSize = BitConverter.ToUInt16(bytes, pe + 20);
            var optional = pe + 24;
            var magic = BitConverter.ToUInt16(bytes, optional);
            var directories = optional + (magic == 0x20B ? 112 : 96);
            var exportRva = BitConverter.ToUInt32(bytes, directories);
            if (exportRva == 0) return names;
            var table = optional + optionalSize;

            int Offset(uint rva)
            {
                for (var i = 0; i < sections; i++)
                {
                    var s = table + i * 40;
                    var va = BitConverter.ToUInt32(bytes, s + 12);
                    var size = Math.Max(BitConverter.ToUInt32(bytes, s + 8), BitConverter.ToUInt32(bytes, s + 16));
                    if (rva >= va && rva < va + size) return (int)(rva - va + BitConverter.ToUInt32(bytes, s + 20));
                }
                return -1;
            }

            var export = Offset(exportRva);
            if (export < 0) return names;
            var count = BitConverter.ToInt32(bytes, export + 0x18);
            var nameTable = Offset(BitConverter.ToUInt32(bytes, export + 0x20));
            if (nameTable < 0) return names;
            for (var i = 0; i < count; i++)
            {
                var at = Offset(BitConverter.ToUInt32(bytes, nameTable + i * 4));
                if (at < 0) continue;
                var end = Array.IndexOf(bytes, (byte)0, at);
                if (end > at) names.Add(Encoding.ASCII.GetString(bytes, at, end - at));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or IndexOutOfRangeException)
        {
            names.Clear();
        }
        return names;
    }
}
