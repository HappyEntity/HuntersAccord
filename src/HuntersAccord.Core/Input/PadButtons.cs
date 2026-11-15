using System.Globalization;
using HuntersAccord.Core.Settings;

namespace HuntersAccord.Core.Input;

/// <summary>The PS4 buttons the launcher lets the player move on the gamepad, in shadPS4's input config names, and the config lines that give them (<see cref="Render"/>).</summary>
public static class PadButtons
{
    /// <summary>PS4 buttons (outputs) in the launcher's order: config name, game text of what it does (<c>GameText.Key.*</c>) and the default gamepad button.</summary>
    public static readonly IReadOnlyList<(string Output, string Action, string DefaultInput)> Outputs =
    [
        ("r1", "Attack", "r1"),
        ("r2", "StrongAttack", "r2"),
        ("l1", "Transform", "l1"),
        ("l2", "Firearm", "l2"),
        ("circle", "Dodge", "circle"),
        ("l3", "LeftStickPress", "l3"),
        ("r3", "LockOn", "r3"),
        ("cross", "Interact", "cross"),
        ("triangle", "BloodVial", "triangle"),
        ("square", "UseItem", "square"),
        ("pad_up", "SwitchTool", "pad_up"),
        ("pad_down", "SwitchItem", "pad_down"),
        ("pad_left", "SwitchLeft", "pad_left"),
        ("pad_right", "SwitchRight", "pad_right"),
        ("options", "Menu", "options"),
        // shadPS4 binds a real touchpad to itself; this is the press of the gamepad's Back / View / Share button.
        ("touchpad_center", "Touchpad", "back"),
    ];

    public static readonly IReadOnlyList<(string Input, string Xbox)> Inputs =
    [
        ("cross", "A"), ("circle", "B"), ("square", "X"), ("triangle", "Y"),
        ("l1", "LB"), ("r1", "RB"), ("l2", "LT"), ("r2", "RT"), ("l3", "LS"), ("r3", "RS"),
        ("options", "Menu"), ("back", "View"),
        ("pad_up", "D-pad"), ("pad_down", "D-pad"), ("pad_left", "D-pad"), ("pad_right", "D-pad"),
    ];

    public static string DefaultInput(string output) => Outputs.First(o => o.Output == output).DefaultInput;

    public static string InputFor(GamepadSettings pad, string output) =>
        pad.Buttons.TryGetValue(output, out var input) && (input == GamepadSettings.Unassigned || IsInput(input))
            ? input
            : DefaultInput(output);

    public static bool IsInput(string input) => Inputs.Any(i => i.Input == input);

    /// <summary>Sets the gamepad button of <paramref name="output"/>, storing only buttons that differ from the default.</summary>
    public static void Assign(GamepadSettings pad, string output, string input)
    {
        if (input == DefaultInput(output)) pad.Buttons.Remove(output);
        else pad.Buttons[output] = input;
    }

    /// <summary>Outputs that share a gamepad button with another one; shadPS4 gives a press to only one binding of a button, so all of them but one do nothing.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> SharedWith(GamepadSettings pad)
    {
        var byInput = Outputs.Select(o => (o.Output, Input: InputFor(pad, o.Output)))
            .Where(o => o.Input != GamepadSettings.Unassigned)
            .GroupBy(o => o.Input)
            .Where(g => g.Count() > 1);
        var shared = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var group in byInput)
        foreach (var (output, _) in group)
            shared[output] = [.. group.Select(o => o.Output).Where(o => o != output)];
        return shared;
    }

    public static void SwapCrossCircle(GamepadSettings pad)
    {
        var cross = InputFor(pad, "cross");
        var circle = InputFor(pad, "circle");
        Assign(pad, "cross", circle);
        Assign(pad, "circle", cross);
    }

    /// <summary>shadPS4's inner dead zone (1..127) of a stick at <paramref name="percent"/> of its travel.</summary>
    public static int DeadZoneValue(int percent) =>
        Math.Clamp((int)Math.Round(Math.Clamp(percent, 0, GamepadSettings.MaxDeadZonePercent) * 127 / 100.0), 1, 127);

    public static IEnumerable<string> Render(GamepadSettings pad)
    {
        foreach (var (output, _, _) in Outputs)
        {
            var input = InputFor(pad, output);
            if (input != GamepadSettings.Unassigned) yield return $"{output} = {input}";
        }

        var (move, look) = pad.SwapSticks ? ("right", "left") : ("left", "right");
        yield return $"axis_left_x = axis_{move}_x";
        yield return $"axis_left_y = axis_{move}_y";
        yield return $"axis_right_x = axis_{look}_x";
        yield return $"axis_right_y = axis_{look}_y";

        // shadPS4 applies a dead zone to the stick it outputs: with the sticks swapped the PS4's left stick is the
        // physical right one.
        var (outLeft, outRight) = pad.SwapSticks
            ? (pad.RightDeadZonePercent, pad.LeftDeadZonePercent)
            : (pad.LeftDeadZonePercent, pad.RightDeadZonePercent);
        yield return $"analog_deadzone = leftjoystick, {DeadZoneValue(outLeft)}, 127";
        yield return $"analog_deadzone = rightjoystick, {DeadZoneValue(outRight)}, 127";

        var (r, g, b) = ParseColor(pad.LightColor);
        yield return $"override_controller_color = {(pad.OwnLightColor ? "true" : "false")}, {r}, {g}, {b}";
    }

    public static (byte R, byte G, byte B) ParseColor(string? color)
    {
        if (color is { Length: 7 } && color[0] == '#'
            && uint.TryParse(color.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return ((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return ParseColor(GamepadSettings.DefaultLightColor);
    }

    public static string FormatColor(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";
}
