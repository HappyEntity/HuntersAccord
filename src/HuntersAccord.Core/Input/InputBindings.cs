namespace HuntersAccord.Core.Input;

/// <summary>
/// One binding: a chord of keys ("LShift+Mouse1") driving an action (<see cref="GameActions"/>) in a context (gameplay or menus).
/// Key names are Hunters' Accord's own, platform-neutral: letters A-Z, digits 0-9, F1-F12, Space, Enter, Escape, Backspace, Delete,
/// Tab, LShift, RShift, LCtrl, RCtrl, LAlt, RAlt, Up, Down, Left, Right, Mouse1-Mouse5, WheelUp, WheelDown.
/// </summary>
public sealed record InputBinding(InputContext Context, string Target, string Keys)
{
    public IReadOnlyList<string> KeyList => Keys.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}

public static class DefaultBindings
{
    /// <summary>Defaults modelled on Dark Souls III's PC key layout, with Bloodborne's actions on their counterparts. Trailing comments: the pad button, then the DS3 entry.</summary>
    public static IReadOnlyList<InputBinding> All { get; } =
    [
        G("move_forward", "W"), G("move_back", "S"), G("move_left", "A"), G("move_right", "D"), // left stick: DS3 run F/B/L/R
        G("camera_up", "I"), G("camera_down", "K"), G("camera_left", "J"), G("camera_right", "L"), // right stick: DS3 none (mouse)
        G("attack", "Mouse1"),                                       // R1: DS3 right hand attack
        G("strong_attack", "LShift+Mouse1"),                         // R2: DS3 right hand strong attack
        G("firearm", "Mouse2"),                                      // L2: DS3 left hand attack (the hunter's left hand is the gun)
        G("transform", "F"),                                         // L1: DS3 two-hand weapon
        G("lock_on", "Q"), G("lock_on", "Mouse3"),                   // R3: DS3 camera reset / lock-on
        G("dodge", "Space"),                                         // circle: DS3 dash / backstep / roll
        G("jump", "LCtrl"),                                          // no pad button: the dodge tapped again in a dash, as on a pad; DS3 jumps with Space while dashing
        G("interact", "E"),                                          // cross: DS3 interact
        G("blood_vial", "R"),                                        // triangle: DS3 use item (the Estus)
        G("use_item", "C"),                                          // square: no DS3 slot (DS3 has one item key)
        G("switch_tool", "Up"), G("switch_tool", "WheelUp"),         // d-pad up: DS3 switch spells
        G("switch_item", "Down"), G("switch_item", "WheelDown"),     // d-pad down: DS3 switch quick items
        G("switch_left", "Left"), G("switch_left", "LShift+WheelDown"),   // d-pad left: DS3 switch left-hand weapon
        G("switch_right", "Right"), G("switch_right", "LShift+WheelUp"),  // d-pad right: DS3 switch right-hand weapon
        G("menu", "Escape"),                                         // options: DS3 open menu
        G("gestures", "G"),                                          // right half of the touchpad: DS3 gesture menu
        G("belongings", "B"),                                        // left half: no DS3 slot
        G("quick_item_1", "1"), G("quick_item_2", "2"), G("quick_item_3", "3"), // a quick item slot used directly: no pad button of
        G("quick_item_4", "4"), G("quick_item_5", "5"), G("quick_item_6", "6"), // its own, no DS3 slot (1 and 3 are also L2 / R2 in menus)

        I("menu_up", "Up"), I("menu_up", "W"), I("menu_down", "Down"), I("menu_down", "S"), // d-pad: DS3 cursor, WASD too
        I("menu_left", "Left"), I("menu_left", "A"), I("menu_right", "Right"), I("menu_right", "D"),
        I("menu_confirm", "E"),                                      // cross: DS3 confirm (its left click is the mod's MenuMouse)
        I("menu_back", "Q"), I("menu_back", "Escape"),               // circle: DS3 cancel; Esc, DS3's menu key, closes it too
        I("menu_tab_left", "LShift+Left"), I("menu_tab_right", "LShift+Right"), // L1 / R1: DS3 switch tab
        I("menu_square", "R"),                                       // DS3 function 1
        I("menu_triangle", "F"),                                     // DS3 function 2 (its default not sourced)
        // No DS3 default for these.
        I("menu_l2", "1"), I("menu_r2", "3"),
        I("menu_touch_left", "T"), I("menu_touch_right", "Y"),       // touchpad halves: prompts KG_TP_L / KG_TP_R
        I("menu_l3", "G"), I("menu_r3", "H"),                        // e.g. R3 = help
        // Right stick: turns the model in character creation (also Mouse 2 dragged there).
        I("menu_rotate_left", "J"), I("menu_rotate_right", "L"), I("menu_rotate_up", "I"), I("menu_rotate_down", "K"),
    ];

    /// <summary>The menus' cursor keys before WASD was added to them: a cursor action saved with exactly these takes today's default.</summary>
    public static IReadOnlyList<InputBinding> CursorBeforeWasd { get; } =
    [
        I("menu_up", "Up"), I("menu_down", "Down"), I("menu_left", "Left"), I("menu_right", "Right"),
    ];

    /// <summary>The defaults before the DS3 layout, named as actions: an action saved with exactly these was never changed and takes the new default.</summary>
    public static IReadOnlyList<InputBinding> Before { get; } =
    [
        G("move_forward", "W"), G("move_back", "S"), G("move_left", "A"), G("move_right", "D"),
        G("camera_up", "I"), G("camera_down", "K"), G("camera_left", "J"), G("camera_right", "L"),
        G("attack", "Mouse1"), G("strong_attack", "LShift+Mouse1"), G("firearm", "Mouse2"), G("transform", "LShift+Mouse2"),
        G("lock_on", "Mouse3"), G("lock_on", "Q"), G("dodge", "Space"), G("jump", "LCtrl"),
        G("interact", "E"), G("interact", "Enter"), G("blood_vial", "R"), G("use_item", "F"),
        G("switch_tool", "Up"), G("switch_tool", "WheelUp"), G("switch_item", "Down"), G("switch_item", "WheelDown"),
        G("switch_left", "Left"), G("switch_right", "Right"), G("menu", "Escape"), G("gestures", "Tab"), G("belongings", "B"),
        G("quick_item_1", "1"), G("quick_item_2", "2"), G("quick_item_3", "3"), // came later: as today
        G("quick_item_4", "4"), G("quick_item_5", "5"), G("quick_item_6", "6"),
        I("menu_up", "W"), I("menu_up", "Up"), I("menu_down", "S"), I("menu_down", "Down"),
        I("menu_left", "A"), I("menu_left", "Left"), I("menu_right", "D"), I("menu_right", "Right"),
        I("menu_confirm", "E"), I("menu_confirm", "Enter"), I("menu_back", "Escape"), I("menu_back", "Backspace"),
        I("menu_triangle", "R"), I("menu_square", "F"), I("menu_tab_left", "Q"), I("menu_tab_right", "Tab"),
        I("menu_l2", "1"), I("menu_r2", "3"), I("menu_touch_left", "T"), I("menu_touch_right", "Y"),
        I("menu_l3", "G"), I("menu_r3", "H"),
        I("menu_rotate_left", "J"), I("menu_rotate_right", "L"), I("menu_rotate_up", "I"), I("menu_rotate_down", "K"),
    ];

    private static InputBinding G(string target, string keys) => new(InputContext.Gameplay, target, keys);
    private static InputBinding I(string target, string keys) => new(InputContext.Interface, target, keys);
}
