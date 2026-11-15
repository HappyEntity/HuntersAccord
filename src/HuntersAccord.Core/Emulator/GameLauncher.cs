using System.Diagnostics;
using HuntersAccord.Core.Game;

namespace HuntersAccord.Core.Emulator;

/// <summary>Builds the shadPS4 command line for Bloodborne with the Hunters' Accord profile and, when available, the Hunters' Accord mod layer.</summary>
public static class GameLauncher
{
    public const string LayerName = "VK_LAYER_HUNTERSACCORD_overlay";
    public const string LayerManifest = "huntersaccord_layer.json";

    public static string? FindModLayerDir()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "mod");
        return File.Exists(Path.Combine(dir, LayerManifest)) ? dir : null;
    }

    /// <param name="player">Local player (1; 2 for a second game instance on this PC, to test co-op).</param>
    /// <param name="hideConsole">Start shadPS4 without its console window (a console program: none is created).</param>
    /// <param name="steam">Co-op over Steam (the mod's Steam transport); null: players on this PC only.</param>
    /// <param name="resolution">The game's render size (<see cref="ScreenResolution"/>); null: the game's own.</param>
    public static ProcessStartInfo CreateStartInfo(string emulatorExe, GameInstall game, EmulatorProfile profile, string? modLayerDir, int player = 1,
        bool hideConsole = false, SteamLaunch? steam = null, ScreenResolution? resolution = null)
    {
        if (!File.Exists(emulatorExe)) throw new FileNotFoundException("shadPS4 executable not found.", emulatorExe);
        if (!File.Exists(game.EbootPath)) throw new FileNotFoundException("eboot.bin not found.", game.EbootPath);
        // shadPS4 on Windows patches thread-block reads ("mov r64, fs:[0]") as each first faults; threads reaching one together crashed at load.
        // Its static patching (0.18+) patches them all ahead, at the module's load.
        profile.UseStaticPatching();
        // A shader cache shadPS4 would ignore (GPU or driver changed, another build) is built again instead.
        profile.KeepShaderCacheUsable(Bloodborne.TitleId, emulatorExe);

        var psi = new ProcessStartInfo(emulatorExe)
        {
            // Portable mode: shadPS4 picks up <working dir>/user.
            WorkingDirectory = profile.Root,
            UseShellExecute = false,
            CreateNoWindow = hideConsole,
        };
        // Only config.json counts: a per-game file (custom_configs/<TITLE_ID>.json, e.g. made with another shadPS4
        // launcher) would quietly override the settings the launcher writes.
        psi.ArgumentList.Add("--config-global");
        // The update folder (<game>-UPDATE) next to the game is found by shadPS4 itself.
        psi.ArgumentList.Add("-g");
        psi.ArgumentList.Add(game.EbootPath);

        if (modLayerDir is not null)
        {
            profile.KeepShadNetOff();
            profile.KeepTrophyPopupOff();
            // The Vulkan loader loads the Hunters' Accord layer into shadPS4: no DLL injection needed, same on every OS.
            psi.Environment["VK_ADD_LAYER_PATH"] = modLayerDir;
            psi.Environment["VK_INSTANCE_LAYERS"] = LayerName;        // older loaders
            psi.Environment["VK_LOADER_LAYERS_ENABLE"] = LayerName;   // loaders >= 1.3.234
            psi.Environment[ModContract.PlayerVariable] = player.ToString(System.Globalization.CultureInfo.InvariantCulture);
            psi.Environment[ModContract.DirectoryAddressVariable] = ModContract.DirectoryLocalAddress;
            psi.Environment[ModContract.ModsDirVariable] = ModContract.ModsDir(game);
            if (resolution is { IsStock: false } size) psi.Environment[ScreenResolution.EnvironmentVariable] = size.ToString();
            if (steam is not null)
            {
                var app = steam.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                psi.Environment[SteamLaunch.TransportVariable] = "steam";
                psi.Environment["SteamAppId"] = app;
                psi.Environment["SteamGameId"] = app;
                if (steam.LibraryPath is not null) psi.Environment[SteamLaunch.LibraryVariable] = steam.LibraryPath;
            }
        }
        return psi;
    }
}

/// <summary>
/// Co-op over Steam for a game start: the FromSoftware game's app id and the steam_api64.dll to load (null when none was
/// found: the mod then says Steam is unavailable rather than fall back to this PC unseen).
/// </summary>
public sealed record SteamLaunch(uint AppId, string? LibraryPath)
{
    public const string TransportVariable = "HA_TRANSPORT";

    public const string LibraryVariable = "HA_STEAM_API";
}
