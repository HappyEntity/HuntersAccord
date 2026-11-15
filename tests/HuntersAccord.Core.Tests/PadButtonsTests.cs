using HuntersAccord.Core.Input;
using HuntersAccord.Core.Settings;

namespace HuntersAccord.Core.Tests;

public sealed class PadButtonsTests
{
    private static List<string> Lines(string ini) =>
        [.. ini.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Where(l => !l.StartsWith('#'))];

    [Fact]
    public void Default_gamepad_passes_every_button_through_as_shadps4_does()
    {
        var lines = Lines(KbmLayout.Render(withMod: true));

        // shadPS4's own default controller part (input_handler.cpp; the same in v0.19.0 and v0.20.0), dead zones and colour included.
        string[] expected =
        [
            "r1 = r1", "r2 = r2", "l1 = l1", "l2 = l2", "circle = circle", "l3 = l3", "r3 = r3", "cross = cross",
            "triangle = triangle", "square = square", "pad_up = pad_up", "pad_down = pad_down", "pad_left = pad_left",
            "pad_right = pad_right", "options = options", "touchpad_center = back",
            "axis_left_x = axis_left_x", "axis_left_y = axis_left_y", "axis_right_x = axis_right_x", "axis_right_y = axis_right_y",
            "analog_deadzone = leftjoystick, 5, 127", "analog_deadzone = rightjoystick, 5, 127",
            "override_controller_color = false, 179, 20, 30",
        ];
        Assert.Equal(expected, lines);
    }

    [Fact]
    public void Swap_cross_and_circle_moves_both_and_marks_them_changed()
    {
        var pad = new GamepadSettings();
        PadButtons.SwapCrossCircle(pad);

        var lines = Lines(KbmLayout.Render(withMod: true, pad));
        Assert.Contains("cross = circle", lines);
        Assert.Contains("circle = cross", lines);
        Assert.Equal(2, pad.Buttons.Count);

        PadButtons.SwapCrossCircle(pad);
        Assert.Empty(pad.Buttons); // back to the defaults, nothing left to save
    }

    [Fact]
    public void An_unassigned_button_is_not_written_and_a_shared_one_is_reported_on_both_rows()
    {
        var pad = new GamepadSettings();
        PadButtons.Assign(pad, "pad_down", GamepadSettings.Unassigned);
        PadButtons.Assign(pad, "square", "r1");

        var lines = Lines(KbmLayout.Render(withMod: true, pad));
        Assert.DoesNotContain(lines, l => l.StartsWith("pad_down "));
        Assert.Contains("square = r1", lines);

        var shared = PadButtons.SharedWith(pad);
        Assert.Equal(["square"], shared["r1"]);
        Assert.Equal(["r1"], shared["square"]);
        Assert.Equal(2, shared.Count);
    }

    [Fact]
    public void Swapped_sticks_carry_their_dead_zones_to_the_stick_shadps4_outputs()
    {
        var pad = new GamepadSettings { SwapSticks = true, LeftDeadZonePercent = 20, RightDeadZonePercent = 4 };

        var lines = Lines(KbmLayout.Render(withMod: true, pad));
        Assert.Contains("axis_left_x = axis_right_x", lines);
        Assert.Contains("axis_right_y = axis_left_y", lines);
        // The physical left stick (20 %) now gives the PS4's right stick, and shadPS4 applies dead zones per output.
        Assert.Contains("analog_deadzone = rightjoystick, 25, 127", lines);
        Assert.Contains("analog_deadzone = leftjoystick, 5, 127", lines);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(4, 5)]
    [InlineData(40, 51)]
    [InlineData(90, 51)]
    public void Dead_zone_percent_maps_to_shadps4_range(int percent, int value) =>
        Assert.Equal(value, PadButtons.DeadZoneValue(percent));

    [Fact]
    public void Own_light_colour_is_written_and_a_broken_one_falls_back_to_the_default()
    {
        var pad = new GamepadSettings { OwnLightColor = true, LightColor = "#7B52C4" };
        Assert.Contains("override_controller_color = true, 123, 82, 196", Lines(KbmLayout.Render(withMod: true, pad)));

        pad.LightColor = "purple";
        Assert.Contains("override_controller_color = true, 179, 20, 30", Lines(KbmLayout.Render(withMod: true, pad)));
    }

    [Fact]
    public void An_unknown_saved_input_keeps_the_default()
    {
        var pad = new GamepadSettings { Buttons = { ["r1"] = "no_such_button" } };
        Assert.Equal("r1", PadButtons.InputFor(pad, "r1"));
    }

    [Fact]
    public void Mod_fallback_adds_the_keyboard_and_keeps_the_players_gamepad()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ha-padtest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pad = new GamepadSettings { SwapSticks = true };
            PadButtons.SwapCrossCircle(pad);
            KbmLayout.Write(dir, "CUSA03173", withMod: true, pad);

            KbmLayout.AddShadPs4Keyboard(dir, "CUSA03173");

            var lines = Lines(File.ReadAllText(KbmLayout.ConfigPath(dir, "CUSA03173")));
            Assert.Contains("axis_left_y_minus = w", lines); // the keyboard is back
            Assert.Contains("cross = circle", lines); // the gamepad stays the player's
            Assert.Contains("axis_left_x = axis_right_x", lines);
            Assert.DoesNotContain("cross = cross", lines);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
