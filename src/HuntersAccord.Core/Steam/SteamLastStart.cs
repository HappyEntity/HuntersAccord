using System.Globalization;

namespace HuntersAccord.Core.Steam;

/// <summary>What the mod found when it last started Steam (written by the mod, shown by the launcher): the app id, whether the player owns it, or why Steam did not start.</summary>
public sealed record SteamLastStart(uint AppId, bool Owned, string? Error)
{
    private static string FilePath => Path.Combine(AppPaths.DataRoot, "steam-last-start.txt");

    public void Write()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            File.WriteAllLines(FilePath,
                [AppId.ToString(CultureInfo.InvariantCulture), Owned ? "owned" : "not owned", Error ?? ""]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Only a hint for the launcher.
        }
    }

    public static SteamLastStart? Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var lines = File.ReadAllLines(FilePath);
            if (lines.Length < 2 || !uint.TryParse(lines[0], CultureInfo.InvariantCulture, out var app)) return null;
            return new SteamLastStart(app, lines[1] == "owned", lines.Length > 2 && lines[2].Length > 0 ? lines[2] : null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
