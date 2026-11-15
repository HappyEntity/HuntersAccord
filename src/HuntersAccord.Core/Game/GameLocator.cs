using System.Text.Json;
using HuntersAccord.Core.Pkg;

namespace HuntersAccord.Core.Game;

/// <summary>An unpacked Bloodborne folder (shadPS4 layout) plus its optional update folder.</summary>
public sealed record GameInstall(string BasePath, string BaseVersion, string? UpdatePath, string? UpdateVersion)
{
    public string EffectiveVersion => UpdateVersion ?? BaseVersion;

    public bool IsSupportedVersion => EffectiveVersion == Bloodborne.SupportedAppVersion;

    public string EbootPath => Path.Combine(BasePath, "eboot.bin");
}

public sealed record GameScanResult(IReadOnlyList<GameInstall> Installs, PkgInfo? BasePkg, PkgInfo? PatchPkg)
{
    public GameInstall? Best => Installs
        .OrderByDescending(i => i.IsSupportedVersion)
        .ThenByDescending(i => i.EffectiveVersion, StringComparer.Ordinal)
        .FirstOrDefault();
}

/// <summary>
/// Finds Bloodborne in the three supported forms: an already installed game, an unpacked dump folder
/// (both are the same folder layout), or PKG files that still need unpacking.
/// </summary>
public static class GameLocator
{
    private static readonly string[] UpdateSuffixes = ["-UPDATE", "-patch"];

    public static GameScanResult Scan(IEnumerable<string> searchDirs)
    {
        var installs = new Dictionary<string, GameInstall>(StringComparer.OrdinalIgnoreCase);
        var pkgs = new List<PkgInfo>();
        var seenPkgs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in searchDirs.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var candidate in Enumerable.Prepend(SafeSubdirectories(dir), dir))
            {
                if (TryReadInstall(candidate) is { } install)
                    installs.TryAdd(Path.GetFullPath(install.BasePath), install);

                foreach (var pkg in SafeFiles(candidate, "*.pkg"))
                {
                    if (!seenPkgs.Add(Path.GetFullPath(pkg))) continue;
                    try
                    {
                        var info = PkgInstaller.Inspect(pkg);
                        if (info.TitleId == Bloodborne.TitleId) pkgs.Add(info);
                    }
                    catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
                    {
                        // Not a readable PS4 package — ignore.
                    }
                }
            }
        }

        var basePkg = pkgs.Where(p => !p.IsPatch).OrderByDescending(p => p.AppVersion, StringComparer.Ordinal).FirstOrDefault();
        var patchPkg = pkgs.Where(p => p.IsPatch).OrderByDescending(p => p.AppVersion, StringComparer.Ordinal).FirstOrDefault();
        return new GameScanResult(installs.Values.ToList(), basePkg, patchPkg);
    }

    public static GameInstall? TryReadInstall(string folder)
    {
        if (UpdateSuffixes.Any(s => folder.TrimEnd('\\', '/').EndsWith(s, StringComparison.OrdinalIgnoreCase)))
            return null;

        var sfo = TryReadSfo(folder);
        if (sfo is null || sfo.TitleId != Bloodborne.TitleId || sfo.IsPatch) return null;
        if (!File.Exists(Path.Combine(folder, "eboot.bin"))) return null;

        foreach (var suffix in UpdateSuffixes)
        {
            var update = folder.TrimEnd('\\', '/') + suffix;
            if (TryReadSfo(update) is { } patch && patch.TitleId == Bloodborne.TitleId)
                return new GameInstall(folder, sfo.AppVersion ?? "", update, patch.AppVersion);
        }
        return new GameInstall(folder, sfo.AppVersion ?? "", null, null);
    }

    /// <summary>Game folders registered in shadPS4's own config (<c>General.install_dirs</c>).</summary>
    public static IEnumerable<string> ReadShadPs4InstallDirs(string shadPs4UserDir)
    {
        var config = Path.Combine(shadPs4UserDir, "config.json");
        if (!File.Exists(config)) return [];
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(config));
            if (!doc.RootElement.TryGetProperty("General", out var general) ||
                !general.TryGetProperty("install_dirs", out var dirs) ||
                dirs.ValueKind != JsonValueKind.Array)
                return [];

            return dirs.EnumerateArray()
                .Where(d => !d.TryGetProperty("enabled", out var en) || en.ValueKind != JsonValueKind.False)
                .Select(d => d.TryGetProperty("path", out var p) ? p.GetString() : null)
                .OfType<string>()
                .ToList();
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            return [];
        }
    }

    private static ParamSfo? TryReadSfo(string folder)
    {
        var path = Path.Combine(folder, "sce_sys", "param.sfo");
        try
        {
            return File.Exists(path) ? ParamSfo.Parse(File.ReadAllBytes(path)) : null;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> SafeSubdirectories(string dir)
    {
        try { return Directory.GetDirectories(dir); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }
    }

    private static IEnumerable<string> SafeFiles(string dir, string pattern)
    {
        try { return Directory.GetFiles(dir, pattern); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }
    }
}
