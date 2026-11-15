using HuntersAccord.Core.Input;
using HuntersAccord.Core.Settings;

namespace HuntersAccord.Core.Tests;

public sealed class ShadPs4HotkeysTests
{
    private static Dictionary<string, string> Lines(string ini) =>
        ini.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !l.StartsWith('#'))
            .Select(l => l.Split('=', 2))
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim());

    [Fact]
    public void Every_shadps4_hotkey_is_written_so_none_gets_its_default_back()
    {
        var lines = Lines(ShadPs4Hotkeys.Render(new HashSet<string>()));

        // shadPS4 0.18-0.20 append any of these that the file lacks, with their default keys.
        string[] all =
        [
            "hotkey_capture_frame", "hotkey_screenshot_with_overlays", "hotkey_fullscreen", "hotkey_show_fps", "hotkey_pause",
            "hotkey_reload_inputs", "hotkey_toggle_mouse_to_joystick", "hotkey_toggle_mouse_to_gyro", "hotkey_add_virtual_user",
            "hotkey_remove_virtual_user", "hotkey_toggle_mouse_to_touchpad", "hotkey_quit", "hotkey_volume_up",
            "hotkey_volume_down", "hotkey_emulator_settings", "hotkey_toggle_friends",
        ];
        Assert.Equal(all.Order(), lines.Keys.Order());
        Assert.Equal("f11", lines["hotkey_fullscreen"]);
        Assert.Equal("lalt, f12", lines["hotkey_screenshot_with_overlays"]);
        Assert.Equal("lctrl, lshift, end", lines["hotkey_quit"]);
        Assert.Equal("unmapped", lines["hotkey_toggle_mouse_to_gyro"]);
        Assert.Equal("unmapped", lines["hotkey_reload_inputs"]);
        // shadPS4 0.20 skips an empty value as "Invalid format" (a warning per line in its log).
        Assert.DoesNotContain(lines, l => l.Value.Length == 0);
    }

    [Fact]
    public void A_hotkey_on_a_key_of_the_mods_controls_is_off()
    {
        var settings = new GameSettings
        {
            Bindings = [new InputBinding(InputContext.Gameplay, "options", "F9")],
            SaveBackupKey = "LShift+F10",
        };
        var keys = ShadPs4Hotkeys.ModKeys(settings);
        var lines = Lines(ShadPs4Hotkeys.Render(keys));

        Assert.Equal("unmapped", lines["hotkey_pause"]);
        Assert.Equal("unmapped", lines["hotkey_show_fps"]);
        Assert.Equal("f11", lines["hotkey_fullscreen"]);
    }

    [Fact]
    public void A_hotkey_the_player_turned_off_has_no_key()
    {
        var lines = Lines(ShadPs4Hotkeys.Render(new HashSet<string>(), ["hotkey_pause", "hotkey_volume_up"]));

        Assert.Equal("unmapped", lines["hotkey_pause"]);
        Assert.Equal("unmapped", lines["hotkey_volume_up"]);
        Assert.Equal("kpminus", lines["hotkey_volume_down"]);
        Assert.Equal("f11", lines["hotkey_fullscreen"]);
    }

    [Fact]
    public void The_default_controls_leave_the_kept_hotkeys_on() =>
        Assert.DoesNotContain(ShadPs4Hotkeys.Kept, h => ShadPs4Hotkeys.IsTaken(h, ShadPs4Hotkeys.ModKeys(new GameSettings())));
}
