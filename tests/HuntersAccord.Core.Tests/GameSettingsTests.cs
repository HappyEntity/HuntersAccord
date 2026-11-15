using System.Text.Json;
using HuntersAccord.Core.Game;
using HuntersAccord.Core.Input;
using HuntersAccord.Core.Settings;

namespace HuntersAccord.Core.Tests;

public sealed class GameSettingsTests
{
    [Fact]
    public void Actions_left_at_the_earlier_defaults_move_to_the_ds3_layout_changed_ones_stay()
    {
        // Bindings saved by an older version (every binding written out, attack changed): unchanged actions take today's defaults.
        var saved = DefaultBindings.Before
            .Where(b => b.Target != "attack")
            .Select(b => b.Context == InputContext.Interface && b.Target == "menu_confirm" ? b with { Target = "cross" } : b)
            .Append(new InputBinding(InputContext.Gameplay, "attack", "Mouse4"))
            .Append(new InputBinding(InputContext.Gameplay, "use_item", "Z")) // changed: F → Z
            .Where(b => b.Target != "use_item" || b.Keys == "Z")
            .ToList();
        var bindings = new GameSettings { Bindings = saved }.EffectiveBindings;

        Assert.Equal(["Mouse4"], KeyBindingActions.KeysOf(bindings, "attack"));
        Assert.Equal(["Z"], KeyBindingActions.KeysOf(bindings, "use_item"));
        Assert.Equal(["F"], KeyBindingActions.KeysOf(bindings, "transform"));           // was L-Shift + Mouse 2
        Assert.Equal(["G"], KeyBindingActions.KeysOf(bindings, "gestures"));
        Assert.Equal(["E"], KeyBindingActions.KeysOf(bindings, "menu_confirm"));
        Assert.Equal(["Up", "W"], KeyBindingActions.KeysOf(bindings, "menu_up"));
        Assert.Equal(KeyBindingActions.KeysOf(DefaultBindings.All, "switch_right"), KeyBindingActions.KeysOf(bindings, "switch_right"));
    }

    [Fact]
    public void Bindings_saved_before_the_quick_item_keys_stay_and_the_new_rows_get_the_digits()
    {
        var saved = DefaultBindings.All.Where(b => !GameActions.IsQuickItem(b.Target) && b.Target != "attack")
            .Append(new InputBinding(InputContext.Gameplay, "attack", "Mouse4"))
            .ToList();
        var bindings = new GameSettings { Bindings = saved, BindingsFormat = GameSettings.CurrentBindingsFormat }.EffectiveBindings;

        Assert.Equal(["Mouse4"], KeyBindingActions.KeysOf(bindings, "attack"));
        for (var slot = 1; slot <= 6; slot++)
            Assert.Equal([slot.ToString()], KeyBindingActions.KeysOf(bindings, $"quick_item_{slot}"));
        Assert.Equal(["1"], KeyBindingActions.KeysOf(bindings, "menu_l2")); // a menu's key too: another context
    }

    [Fact]
    public void A_digit_the_player_gave_to_a_world_action_stays_its_quick_item_row_is_empty()
    {
        var saved = DefaultBindings.All.Where(b => !GameActions.IsQuickItem(b.Target) && b.Target != "gestures")
            .Append(new InputBinding(InputContext.Gameplay, "gestures", "2"))
            .ToList();
        var bindings = new GameSettings { Bindings = saved, BindingsFormat = GameSettings.CurrentBindingsFormat }.EffectiveBindings;

        Assert.Equal(["2"], KeyBindingActions.KeysOf(bindings, "gestures"));
        Assert.Empty(KeyBindingActions.KeysOf(bindings, "quick_item_2"));
        Assert.Equal(["3"], KeyBindingActions.KeysOf(bindings, "quick_item_3"));
        Assert.Empty(KeyBindingActions.SharedKeys(bindings));
    }

    [Fact]
    public void Every_action_has_default_keys_and_no_key_does_two_actions_of_one_context()
    {
        Assert.All(GameActions.All, a => Assert.NotEmpty(KeyBindingActions.KeysOf(DefaultBindings.All, a.Id)));
        Assert.All(GameActions.All, a => Assert.NotEmpty(KeyBindingActions.KeysOf(DefaultBindings.Before, a.Id)));
        Assert.Empty(KeyBindingActions.SharedKeys(DefaultBindings.All, InputContext.Gameplay));
        Assert.Empty(KeyBindingActions.SharedKeys(DefaultBindings.All, InputContext.Interface));
        Assert.All(DefaultBindings.All, b => Assert.Equal(b.Context, GameActions.Find(b.Target)!.Context));
    }

    [Fact]
    public void Settings_from_before_the_hunters_accord_section_keep_keyboard_mouse_and_key_prompts_on()
    {
        const string old = """{ "MouseCamera": false, "SkipLogos": true, "EnabledPatches": ["fps60_ha"] }""";
        var settings = JsonSerializer.Deserialize(old, GameSettingsJsonContext.Default.GameSettings)!;

        Assert.True(settings.KeyboardMouse);
        Assert.True(settings.KeyPrompts);
        Assert.True(settings.ShowKeyPrompts);
        Assert.False(settings.MouseCamera);
        // The patches as saved.
        Assert.Equal(new[] { "fps60_ha" }, settings.Patches);
    }

    [Fact]
    public void Key_prompts_show_only_with_keyboard_and_mouse_play()
    {
        Assert.False(new GameSettings { KeyboardMouse = false, KeyPrompts = true }.ShowKeyPrompts);
        Assert.False(new GameSettings { KeyboardMouse = true, KeyPrompts = false }.ShowKeyPrompts);

        var json = JsonSerializer.Serialize(new GameSettings { KeyboardMouse = false, KeyPrompts = false },
            GameSettingsJsonContext.Default.GameSettings);
        var read = JsonSerializer.Deserialize(json, GameSettingsJsonContext.Default.GameSettings)!;
        Assert.False(read.KeyboardMouse);
        Assert.False(read.KeyPrompts);
    }

    [Fact]
    public void The_automatic_backup_banner_is_on_by_default_and_saved_when_turned_off()
    {
        Assert.True(new GameSettings().SaveBackupAutoBanner);
        // Settings saved before the setting existed keep the banner.
        Assert.True(JsonSerializer.Deserialize("""{ "SaveBackupEveryMinutes": 10 }""", GameSettingsJsonContext.Default.GameSettings)!.SaveBackupAutoBanner);

        var json = JsonSerializer.Serialize(new GameSettings { SaveBackupAutoBanner = false },
            GameSettingsJsonContext.Default.GameSettings);
        Assert.False(JsonSerializer.Deserialize(json, GameSettingsJsonContext.Default.GameSettings)!.SaveBackupAutoBanner);
    }

    [Fact]
    public void Bindings_saved_with_pad_buttons_become_their_actions()
    {
        const string saved = """
            { "Bindings": [
                { "Context": "Gameplay", "Target": "r1", "Keys": "Mouse4" },
                { "Context": "Gameplay", "Target": "dpad_right", "Keys": "X" },
                { "Context": "Gameplay", "Target": "touchpad", "Keys": "G" },
                { "Context": "Gameplay", "Target": "move_forward", "Keys": "Up" },
                { "Context": "Interface", "Target": "cross", "Keys": "F12" }
            ] }
            """;
        var settings = JsonSerializer.Deserialize(saved, GameSettingsJsonContext.Default.GameSettings)!;
        var bindings = settings.EffectiveBindings;

        Assert.Equal(["Mouse4"], KeyBindingActions.KeysOf(bindings, "attack"));
        Assert.Equal(["X"], KeyBindingActions.KeysOf(bindings, "switch_right"));
        Assert.Equal(["G"], KeyBindingActions.KeysOf(bindings, "gestures"));
        Assert.Equal(["Up"], KeyBindingActions.KeysOf(bindings, "move_forward"));
        // Actions not saved take their defaults; no pad names are left among the gameplay bindings.
        Assert.Equal(KeyBindingActions.KeysOf(DefaultBindings.All, "dodge"), KeyBindingActions.KeysOf(bindings, "dodge"));
        Assert.All(bindings, b => Assert.Equal(b.Context, GameActions.Find(b.Context, b.Target)!.Context));
        Assert.All(bindings, b => Assert.NotNull(GameActions.Find(b.Target)));
        // Menu bindings saved with pad names are the menus' actions now.
        Assert.Equal(["F12"], KeyBindingActions.KeysOf(bindings, "menu_confirm"));
    }

    [Fact]
    public void Two_keys_per_action_are_saved_and_loaded()
    {
        var settings = new GameSettings();
        settings.SetBindings(KeyBindingActions.Rebind(settings.EffectiveBindings, "lock_on", ["Mouse4", "LCtrl+Q"]));
        var json = JsonSerializer.Serialize(settings, GameSettingsJsonContext.Default.GameSettings);
        var loaded = JsonSerializer.Deserialize(json, GameSettingsJsonContext.Default.GameSettings)!;

        Assert.Equal(GameSettings.CurrentBindingsFormat, loaded.BindingsFormat);
        Assert.Equal(["Mouse4", "LCtrl+Q"], KeyBindingActions.KeysOf(loaded.EffectiveBindings, "lock_on"));
        Assert.Equal(KeyBindingActions.KeysOf(DefaultBindings.All, "attack"), KeyBindingActions.KeysOf(loaded.EffectiveBindings, "attack"));
    }

    [Fact]
    public void A_file_saved_with_one_key_per_action_still_loads()
    {
        // Written by the single-key screen: one binding per changed action, no BindingsFormat.
        const string saved = """
            { "Bindings": [
                { "Context": "Gameplay", "Target": "lock_on", "Keys": "Mouse4" },
                { "Context": "Gameplay", "Target": "attack", "Keys": "LShift+Mouse5" },
                { "Context": "Interface", "Target": "menu_confirm", "Keys": "Enter" }
            ] }
            """;
        var settings = JsonSerializer.Deserialize(saved, GameSettingsJsonContext.Default.GameSettings)!;
        var bindings = settings.EffectiveBindings;

        Assert.Equal(0, settings.BindingsFormat);
        Assert.Equal(["Mouse4"], KeyBindingActions.KeysOf(bindings, "lock_on"));
        Assert.Equal(["LShift+Mouse5"], KeyBindingActions.KeysOf(bindings, "attack"));
        Assert.Equal(["Enter"], KeyBindingActions.KeysOf(bindings, "menu_confirm"));
        Assert.Equal(KeyBindingActions.KeysOf(DefaultBindings.All, "dodge"), KeyBindingActions.KeysOf(bindings, "dodge"));
    }

    [Fact]
    public void The_menu_cursor_takes_WASD_unless_the_player_chose_the_arrow_alone()
    {
        Assert.Equal(["Up", "W"], KeyBindingActions.KeysOf(DefaultBindings.All, "menu_up"));
        // Saved before the cursor took WASD (format 2): the arrow alone was the default, today's arrow and WASD replace it.
        List<InputBinding> arrowsOnly =
        [
            .. DefaultBindings.All.Where(b => b.Target is not ("menu_up" or "menu_down" or "menu_left" or "menu_right")),
            .. DefaultBindings.CursorBeforeWasd,
        ];
        var before = new GameSettings { Bindings = arrowsOnly, BindingsFormat = 2 }.EffectiveBindings;
        Assert.Equal(["Up", "W"], KeyBindingActions.KeysOf(before, "menu_up"));
        Assert.Equal(["Right", "D"], KeyBindingActions.KeysOf(before, "menu_right"));
        // Saved today: the arrow alone is the player's choice.
        var chosen = new GameSettings { Bindings = arrowsOnly, BindingsFormat = GameSettings.CurrentBindingsFormat }.EffectiveBindings;
        Assert.Equal(["Up"], KeyBindingActions.KeysOf(chosen, "menu_up"));
    }

    [Fact]
    public void Two_keys_the_player_set_are_kept_even_when_they_were_the_old_defaults()
    {
        // E / Enter on interact were Hunters' Accord 0.1's defaults: an old file with them takes today's (E alone), a file
        // the two-key screen wrote keeps them (the player chose them).
        List<InputBinding> withEnter = [.. KeyBindingActions.Rebind(DefaultBindings.All, "interact", ["E", "Enter"])];
        Assert.Equal(["E"], KeyBindingActions.KeysOf(new GameSettings { Bindings = withEnter }.EffectiveBindings, "interact"));

        var settings = new GameSettings();
        settings.SetBindings(withEnter);
        Assert.Equal(["E", "Enter"], KeyBindingActions.KeysOf(settings.EffectiveBindings, "interact"));
    }

    [Fact]
    public void Without_keyboard_and_mouse_play_shadps4_hotkeys_keep_the_keys_of_the_bindings()
    {
        var settings = new GameSettings { Bindings = [new InputBinding(InputContext.Gameplay, "options", "F9")] };
        Assert.Contains("F9", ShadPs4Hotkeys.ModKeys(settings));

        settings.KeyboardMouse = false;
        var keys = ShadPs4Hotkeys.ModKeys(settings);
        Assert.DoesNotContain("F9", keys);
        Assert.Contains(settings.DebugToggleKey, keys); // debug keys stay the mod's
    }

}
