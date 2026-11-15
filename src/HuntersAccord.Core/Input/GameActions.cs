namespace HuntersAccord.Core.Input;

/// <summary>
/// An action keys are bound to (a binding's <see cref="InputBinding.Target"/>): in the world (<see cref="InputContext.Gameplay"/>) or the menus
/// (<see cref="InputContext.Interface"/>). <paramref name="PadTarget"/> is the PS4 pad button or stick direction that does it on a gamepad.
/// <paramref name="QuickSlot"/>: an action of one quick item slot (0-based). Its key does not simply press <paramref name="PadTarget"/>:
/// the slot is chosen first, and an empty slot presses nothing.
/// </summary>
public sealed record GameAction(string Id, string PadTarget, InputContext Context = InputContext.Gameplay, int? QuickSlot = null);

/// <summary>
/// Bloodborne's actions keys are bound to: the world's (attack, weapon changes, ...) and the menus' (cursor, confirm, back, ...).
/// A key gives the game the virtual inputs of its action's button, so a menu key never reaches the world and a world key never moves a menu.
/// </summary>
public static class GameActions
{
    public static IReadOnlyList<GameAction> All { get; } =
    [
        W("move_forward", "move_forward"), W("move_back", "move_back"),
        W("move_left", "move_left"), W("move_right", "move_right"),
        W("camera_up", "camera_up"), W("camera_down", "camera_down"),
        W("camera_left", "camera_left"), W("camera_right", "camera_right"),
        W("attack", "r1"),
        W("strong_attack", "r2"),
        W("transform", "l1"),
        W("firearm", "l2"),
        W("dodge", "circle"),
        W("interact", "cross"),
        W("blood_vial", "triangle"),
        W("use_item", "square"),
        W("switch_tool", "dpad_up"),
        W("switch_right", "dpad_right"),
        W("switch_left", "dpad_left"),
        W("switch_item", "dpad_down"),
        W("lock_on", "r3"),
        W("jump", "l3"), // the mod makes the key the game's own jump (the dodge tapped again in a dash), not a jump button
        W("menu", "options"),
        W("gestures", "touchpad_right"),
        W("belongings", "touchpad_left"),
        Q(0), Q(1), Q(2), Q(3), Q(4), Q(5),

        M("menu_up", "dpad_up"), M("menu_down", "dpad_down"),
        M("menu_left", "dpad_left"), M("menu_right", "dpad_right"),
        M("menu_confirm", "cross"),
        M("menu_back", "circle"),
        M("menu_triangle", "triangle"),
        M("menu_square", "square"),
        M("menu_tab_left", "l1"), M("menu_tab_right", "r1"),
        M("menu_l2", "l2"), M("menu_r2", "r2"),
        M("menu_l3", "l3"), M("menu_r3", "r3"),
        M("menu_touch_left", "touchpad_left"), M("menu_touch_right", "touchpad_right"),
        // The right stick: turns the model in character creation.
        M("menu_rotate_up", "camera_up"), M("menu_rotate_down", "camera_down"),
        M("menu_rotate_left", "camera_left"), M("menu_rotate_right", "camera_right"),
    ];

    private static GameAction W(string id, string padTarget) => new(id, padTarget);
    private static GameAction M(string id, string padTarget) => new(id, padTarget, InputContext.Interface);
    private static GameAction Q(int slot) => new($"{QuickItemPrefix}{slot + 1}", "square", QuickSlot: slot);

    private const string QuickItemPrefix = "quick_item_";

    /// <summary>Whether an action is that of a quick item slot.</summary>
    public static bool IsQuickItem(string id) => id.StartsWith(QuickItemPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Targets saved before bindings named actions (they named the pad button): old id to action, per context. "touchpad" is older still.
    /// </summary>
    private static readonly Dictionary<string, string> FormerGameplayTargets = new(StringComparer.Ordinal)
    {
        ["r1"] = "attack", ["r2"] = "strong_attack", ["l1"] = "transform", ["l2"] = "firearm",
        ["circle"] = "dodge", ["cross"] = "interact", ["triangle"] = "blood_vial", ["square"] = "use_item",
        ["dpad_up"] = "switch_tool", ["dpad_right"] = "switch_right", ["dpad_left"] = "switch_left", ["dpad_down"] = "switch_item",
        ["r3"] = "lock_on", ["l3"] = "jump", ["options"] = "menu",
        ["touchpad"] = "gestures", ["touchpad_right"] = "gestures", ["touchpad_left"] = "belongings",
    };

    private static readonly Dictionary<string, string> FormerInterfaceTargets = new(StringComparer.Ordinal)
    {
        ["dpad_up"] = "menu_up", ["dpad_down"] = "menu_down", ["dpad_left"] = "menu_left", ["dpad_right"] = "menu_right",
        ["cross"] = "menu_confirm", ["circle"] = "menu_back", ["triangle"] = "menu_triangle", ["square"] = "menu_square",
        ["l1"] = "menu_tab_left", ["r1"] = "menu_tab_right", ["l2"] = "menu_l2", ["r2"] = "menu_r2",
        ["l3"] = "menu_l3", ["r3"] = "menu_r3",
        ["touchpad"] = "menu_touch_right", ["touchpad_left"] = "menu_touch_left", ["touchpad_right"] = "menu_touch_right",
        ["camera_up"] = "menu_rotate_up", ["camera_down"] = "menu_rotate_down",
        ["camera_left"] = "menu_rotate_left", ["camera_right"] = "menu_rotate_right",
    };

    private static readonly Dictionary<string, GameAction> ById = All.ToDictionary(a => a.Id, StringComparer.Ordinal);

    public static GameAction? Find(string id) => ById.GetValueOrDefault(id);

    /// <summary>The action of a binding's target in its context (a saved pad button's name maps to the action it does there), or null for an unknown target or an action of the other context.</summary>
    public static GameAction? Find(InputContext context, string target) =>
        Find(Migrate(context, target)) is { } action && action.Context == context ? action : null;

    /// <summary>A target as an action id: a former pad button's name is moved to its action in that context, others stay as they are.</summary>
    public static string Migrate(InputContext context, string target) =>
        (context == InputContext.Gameplay ? FormerGameplayTargets : FormerInterfaceTargets).GetValueOrDefault(target, target);

    /// <summary>The first world action a pad target does (key prompts and "[cross]" marks in texts), or null.</summary>
    public static GameAction? OfPadTarget(string padTarget) =>
        All.FirstOrDefault(a => a.Context == InputContext.Gameplay && a.PadTarget == padTarget);

    /// <summary>The pad target a binding's action is on, or null for an unknown target.</summary>
    public static string? PadTargetOf(InputBinding binding) => Find(binding.Context, binding.Target)?.PadTarget;
}
