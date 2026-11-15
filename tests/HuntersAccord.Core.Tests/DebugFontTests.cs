using System.IO.Compression;
using HuntersAccord.Core.Game;
using HuntersAccord.Core.Patches;

namespace HuntersAccord.Core.Tests;

public sealed class DebugFontTests
{
    [Fact]
    public void Downloaded_dvdroot_is_laid_over_the_game_in_the_mods_folder()
    {
        var root = Directory.CreateTempSubdirectory("ha-font").FullName;
        try
        {
            var game = new GameInstall(Path.Combine(root, "CUSA03173"), "01.00", null, null);
            Directory.CreateDirectory(game.BasePath);
            var zipPath = Path.Combine(root, "download.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var name in new[] { "Debug Menu/dvdroot_ps4/adhoc/font/DbgFont14h.ccm", "Debug Menu/dvdroot_ps4/adhoc/GUIShader/GXGuiShader_Tex.ppo", "Debug Menu/dvdroot_ps4/font/DbgFont14h.ccm.dcx", "Debug Menu/readme.txt" })
                    using (var w = new StreamWriter(zip.CreateEntry(name).Open())) w.Write(name);
            }

            Assert.False(DebugFont.IsInstalled(game));
            Assert.True(DebugFont.Install(game, zipPath));

            Assert.True(DebugFont.IsInstalled(game));
            var data = Path.Combine(ModContract.ModsDir(game), "dvdroot_ps4");
            var files = Directory.GetFiles(data, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(data, f).Replace(Path.DirectorySeparatorChar, '/')).Order(StringComparer.OrdinalIgnoreCase);
            Assert.Equal(["adhoc/font/DbgFont14h.ccm", "adhoc/GUIShader/GXGuiShader_Tex.ppo", "font/DbgFont14h.ccm.dcx"], files);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_file_without_the_font_is_refused()
    {
        var root = Directory.CreateTempSubdirectory("ha-font").FullName;
        try
        {
            var game = new GameInstall(Path.Combine(root, "CUSA03173"), "01.00", null, null);
            File.WriteAllText(Path.Combine(root, "other.ccm"), "x");
            Assert.False(DebugFont.Install(game, Path.Combine(root, "other.ccm")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
