using System.IO.Compression;
using HuntersAccord.Core.Game;

namespace HuntersAccord.Core.Patches;

/// <summary>
/// The debug menu patch needs the debug font and shaders of the development builds (adhoc:/font/DbgFont14h.ccm, ...), which retail Bloodborne lacks;
/// without them the game crashes or the menu closes. Players get them from a Nexus Mods page (not shipped or downloaded by Hunters' Accord).
/// </summary>
public static class DebugFont
{
    public const string DownloadUrl = "https://www.nexusmods.com/bloodborne/mods/253";

    /// <summary>The file the patch loads (adhoc:/font/DbgFont14h.ccm), relative to the game data root.</summary>
    public const string FontFile = "adhoc/font/DbgFont14h.ccm";

    private const string DataRoot = "dvdroot_ps4";

    /// <summary>Lays the downloaded files over the game in shadPS4's mods folder; the whole dvdroot_ps4 folder of <paramref name="source"/> (the .zip or any unpacked file) is copied. False when there is no DbgFont14h.ccm.</summary>
    public static bool Install(GameInstall game, string source)
    {
        var target = Path.Combine(ModContract.ModsDir(game), DataRoot);
        var fontName = Path.GetFileName(FontFile);
        if (source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(source);
            var font = zip.Entries.FirstOrDefault(e => e.Name.Equals(fontName, StringComparison.OrdinalIgnoreCase));
            if (font is null) return false;
            // Everything under the font's dvdroot_ps4 (or, without one, the folder that holds adhoc/font).
            var root = RootOf(font.FullName.Replace('\\', '/'));
            foreach (var entry in zip.Entries)
            {
                var path = entry.FullName.Replace('\\', '/');
                if (entry.Name.Length == 0 || !path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                var file = Path.Combine(target, path[root.Length..]);
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                entry.ExtractToFile(file, overwrite: true);
            }
            return true;
        }

        var start = File.Exists(source) ? Path.GetDirectoryName(source)! : source;
        var fontPath = File.Exists(Path.Combine(start, fontName))
            ? Path.Combine(start, fontName)
            : Directory.EnumerateFiles(start, fontName, SearchOption.AllDirectories).FirstOrDefault();
        if (fontPath is null) return false;
        var sourceRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fontPath)!, "..", ".."));
        if (!Path.GetFileName(sourceRoot).Equals(DataRoot, StringComparison.OrdinalIgnoreCase))
            sourceRoot = Path.GetDirectoryName(Path.GetDirectoryName(fontPath))!; // adhoc/font without dvdroot_ps4 above
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(target, Path.GetRelativePath(sourceRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to, overwrite: true);
        }
        return true;
    }

    private static string RootOf(string fontPath)
    {
        var marker = "/" + DataRoot + "/";
        var at = ("/" + fontPath).LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (at >= 0) return fontPath[..(at + marker.Length - 1)];
        // No dvdroot_ps4 in the archive: the font sits in .../adhoc/font/, the root is above adhoc.
        var fontDir = fontPath[..fontPath.LastIndexOf('/')];
        var adhocDir = fontDir[..Math.Max(0, fontDir.LastIndexOf('/'))];
        var rootEnd = adhocDir.LastIndexOf('/');
        return rootEnd < 0 ? "" : adhocDir[..(rootEnd + 1)];
    }

    public static bool IsInstalled(GameInstall game) =>
        new[] { game.BasePath, game.UpdatePath, ModContract.ModsDir(game) }
            .OfType<string>()
            .Any(root => File.Exists(Path.Combine(root, DataRoot, FontFile)));
}
