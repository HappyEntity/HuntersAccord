using System.Diagnostics;
using HuntersAccord.Core.Game;
using HuntersAccord.Core.Input;
using HuntersAccord.Core.Settings;

namespace HuntersAccord.Core.Emulator;

/// <summary>
/// A running shadPS4 process with the Hunters' Accord profile. Keyboard and mouse are handled inside the game by the mod,
/// so shadPS4's own input config only passes a gamepad through.
/// </summary>
public sealed class EmulatorSession : IDisposable
{
    private EmulatorSession(Process process) => Process = process;

    public Process Process { get; }

    /// <param name="modLayerDir">Hunters' Accord layer folder; null starts plain shadPS4 (no layer) for troubleshooting.</param>
    /// <param name="player">Local player: 2 is a second instance with its own profile, to test co-op on one PC.</param>
    /// <param name="hideConsole">Start shadPS4 without its console window.</param>
    /// <param name="hotkeysOff">shadPS4 hotkeys the player turned off (config names).</param>
    /// <param name="gamepad">The player's gamepad buttons, sticks and light bar; null = as they are.</param>
    /// <param name="resolution">The game's render size; null: the game's own 1920x1080.</param>
    public static EmulatorSession Start(string emulatorExe, GameInstall game, EmulatorProfile profile, string? modLayerDir, int player = 1,
        bool hideConsole = false, IReadOnlyCollection<string>? hotkeysOff = null, SteamLaunch? steam = null, GamepadSettings? gamepad = null,
        Game.ScreenResolution? resolution = null)
    {
        PrepareInput(profile, modLayerDir, hotkeysOff, gamepad);

        // shadPS4 starts its log over: the last run's (a crash's, when the game is started again) kept as .old.
        profile.KeepLastLog();

        var psi = GameLauncher.CreateStartInfo(emulatorExe, game, profile, modLayerDir, player, hideConsole, steam, resolution);
        var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start shadPS4.");
        return new EmulatorSession(process);
    }

    /// <summary>The profile's shadPS4 input as a start needs it: with the mod only the gamepad passes through, since the default.ini keyboard keys would play beside the mod's.</summary>
    public static void PrepareInput(EmulatorProfile profile, string? modLayerDir, IReadOnlyCollection<string>? hotkeysOff = null,
        GamepadSettings? gamepad = null)
    {
        // Per-game input config: a unified config (shadPS4's default) would make shadPS4 read default.ini instead of
        // <TITLE_ID>.ini: its keyboard keys (touchpad = space, circle = b, the sticks on WASD/IJKL...) beside the mod's.
        profile.SetConfigValue("Input", "use_unified_input_config", false);
        KbmLayout.Write(profile.UserDir, Bloodborne.TitleId, withMod: modLayerDir is not null, gamepad);
        ShadPs4Hotkeys.Write(profile.UserDir, modLayerDir is null ? new HashSet<string>() : ShadPs4Hotkeys.ModKeys(GameSettings.Load()), hotkeysOff);
    }

    public void Dispose() => Process.Dispose();
}
