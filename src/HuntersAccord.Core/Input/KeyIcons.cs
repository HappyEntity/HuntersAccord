namespace HuntersAccord.Core.Input;

/// <summary>
/// Key icons for the game's prompts (Resources/KeyIcons): each is a texture in menu/common.tpf and an image export in the menu movies named
/// <see cref="TexturePrefix"/> + its name. The game sizes an inline image only when its texture's name starts with KG_, hence the prefix.
/// </summary>
public static class KeyIcons
{
    public const string TexturePrefix = "KG_HA_";

    public const string ChordJoin = "+";

    /// <summary>The inline image of an icon, as the game's own prompts are made ("&lt;img src='KG_OK' vspace='-8'&gt;").</summary>
    public static string Image(string icon) => $"<img src='{TexturePrefix}{icon}' vspace='-8'>";

    /// <summary>The icon of one of Hunters' Accord's key names (<see cref="InputBinding"/>), or null for a name without one.</summary>
    public static string? IconOfKey(string key) => key switch
    {
        { Length: 1 } when char.IsAsciiLetterUpper(key[0]) || char.IsAsciiDigit(key[0]) => "Key_" + key,
        "F1" or "F2" or "F3" or "F4" or "F5" or "F6" or "F7" or "F8" or "F9" or "F10" or "F11" or "F12" => "Key_" + key,
        "Up" or "Down" or "Left" or "Right" or "Space" or "Enter" or "Backspace" or "Delete" or "Tab"
            or "LShift" or "RShift" or "LCtrl" or "RCtrl" or "LAlt" or "RAlt" => "Key_" + key,
        "Escape" => "Key_Esc",
        "Mouse1" => "Mouse_Left",
        "Mouse2" => "Mouse_Right",
        "Mouse3" => "Mouse_Middle",
        "Mouse4" => "Mouse_Button4",
        "Mouse5" => "Mouse_Button5",
        "WheelUp" => "Mouse_WheelUp",
        "WheelDown" => "Mouse_WheelDown",
        _ => null,
    };

    public const string Arrows = "Group_Arrows";
    public const string ArrowsUpDown = "Group_ArrowsUD";
    public const string ArrowsLeftRight = "Group_ArrowsLR";
    public const string Wasd = "Group_WASD";
    public const string MouseMove = "Group_MouseMove";

    /// <summary>The group icon for a prompt's direction set when its keys are the usual group (the arrows, W A S D, up/down, left/right, or the mouse for the camera); null otherwise.</summary>
    public static string? GroupOf(IReadOnlyList<string> targets, IReadOnlyList<string> chords, InputContext context, bool mouseCamera)
    {
        if (targets.Count > 1 && targets[0] == "camera_up")
            return context == InputContext.Gameplay && mouseCamera ? MouseMove : null;
        if (targets.Count != chords.Count) return null;
        string[] keys = [.. chords];
        return keys switch
        {
            ["Up", "Left", "Down", "Right"] => Arrows,
            ["W", "A", "S", "D"] => Wasd,
            ["Up", "Down"] => ArrowsUpDown,
            ["Left", "Right"] => ArrowsLeftRight,
            _ => null,
        };
    }
}
