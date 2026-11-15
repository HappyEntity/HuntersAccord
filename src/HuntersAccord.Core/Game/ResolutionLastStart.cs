using System.Globalization;

namespace HuntersAccord.Core.Game;

/// <summary>Whether the last start got the launcher's render size: written by the mod, shown by the launcher; one file per local player.</summary>
public sealed record ResolutionLastStart(ScreenResolution Requested, bool Applied, string? Reason)
{
    /// <summary>The reason when the mod came in after the game had taken its render size (a slow start).</summary>
    public const string TooLate = "late";

    /// <summary>The reason when the game's code is not what the mod expects (another build, a patch).</summary>
    public const string Unexpected = "unexpected";

    /// <summary>The reason when the size went in but the game did not take it, or the write failed.</summary>
    public const string Failed = "failed";

    public static string FilePath(int player) =>
        Path.Combine(AppPaths.DataRoot, player <= 1 ? "resolution-last-start.txt" : $"resolution-last-start-{player}.txt");

    public void Write(int player)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            File.WriteAllLines(FilePath(player), [Requested.ToString(), Applied ? "applied" : "stock", Reason ?? ""]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Only a hint for the launcher.
        }
    }

    /// <summary>The player's last start's, if written at or after <paramref name="since"/> (local time); else null.</summary>
    public static ResolutionLastStart? Read(int player, DateTime since)
    {
        try
        {
            var file = new FileInfo(FilePath(player));
            if (!file.Exists || file.LastWriteTime < since) return null;
            var lines = File.ReadAllLines(file.FullName);
            if (lines.Length < 2 || !ScreenResolution.TryParse(lines[0], out var requested)) return null;
            return new ResolutionLastStart(requested, lines[1] == "applied", lines.Length > 2 && lines[2].Length > 0 ? lines[2] : null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Requested} {(Applied ? "applied" : "stock")}{(Reason is null ? "" : " (" + Reason + ")")}");
}
