namespace HuntersAccord.Core.Input;

/// <summary>
/// Actions shown on the in-game key binding screen, grouped into sections. Each row rebinds every binding of its action; a key may do one
/// world action and one menu action (E: interact and confirm), never two of either. The option screens do not scroll, so one section
/// is shown at a time (up to <see cref="RowsPerSection"/> key rows).
/// </summary>
public static class KeyBindingActions
{
    /// <summary>
    /// Key rows of the screen: the longest section. Defaults always takes the slot after them, so the row count never changes while it is open.
    /// </summary>
    public const int RowsPerSection = 14;

    public static IReadOnlyList<(string TextKey, IReadOnlyList<(string Target, string TextKey)> Actions)> Sections { get; } =
    [
        ("GameText.KeysPage.MovementCamera",
        [
            ("move_forward", "GameText.Key.MoveForward"),
            ("move_back", "GameText.Key.MoveBack"),
            ("move_left", "GameText.Key.MoveLeft"),
            ("move_right", "GameText.Key.MoveRight"),
            ("dodge", "GameText.Key.Dodge"),
            ("jump", "GameText.Key.Jump"),
            ("camera_up", "GameText.Key.CameraUp"),
            ("camera_down", "GameText.Key.CameraDown"),
            ("camera_left", "GameText.Key.CameraLeft"),
            ("camera_right", "GameText.Key.CameraRight"),
            ("lock_on", "GameText.Key.LockOn"),
        ]),
        ("GameText.KeysPage.CombatItems",
        [
            ("attack", "GameText.Key.Attack"),
            ("strong_attack", "GameText.Key.StrongAttack"),
            ("transform", "GameText.Key.Transform"),
            ("firearm", "GameText.Key.Firearm"),
            ("switch_right", "GameText.Key.SwitchRight"),
            ("switch_left", "GameText.Key.SwitchLeft"),
            ("blood_vial", "GameText.Key.BloodVial"),
            ("use_item", "GameText.Key.UseItem"),
            ("switch_tool", "GameText.Key.SwitchTool"),
            ("switch_item", "GameText.Key.SwitchItem"),
            ("interact", "GameText.Key.Interact"),
            ("gestures", "GameText.Key.Gestures"),
            ("belongings", "GameText.Key.Belongings"),
            ("menu", "GameText.Key.Menu"),
        ]),
        ("GameText.KeysPage.QuickItems",
        [
            ("quick_item_1", "GameText.Key.QuickItem1"),
            ("quick_item_2", "GameText.Key.QuickItem2"),
            ("quick_item_3", "GameText.Key.QuickItem3"),
            ("quick_item_4", "GameText.Key.QuickItem4"),
            ("quick_item_5", "GameText.Key.QuickItem5"),
            ("quick_item_6", "GameText.Key.QuickItem6"),
        ]),
        ("GameText.KeysPage.MenuNavigation",
        [
            ("menu_up", "GameText.Key.MenuUp"),
            ("menu_down", "GameText.Key.MenuDown"),
            ("menu_left", "GameText.Key.MenuLeft"),
            ("menu_right", "GameText.Key.MenuRight"),
            ("menu_confirm", "GameText.Key.MenuConfirm"),
            ("menu_back", "GameText.Key.MenuBack"),
            ("menu_tab_left", "GameText.Key.MenuTabLeft"),
            ("menu_tab_right", "GameText.Key.MenuTabRight"),
            ("menu_triangle", "GameText.Key.MenuTriangle"),
            ("menu_square", "GameText.Key.MenuSquare"),
        ]),
        ("GameText.KeysPage.MenuMore",
        [
            ("menu_l2", "GameText.Key.MenuL2"),
            ("menu_r2", "GameText.Key.MenuR2"),
            ("menu_l3", "GameText.Key.MenuL3"),
            ("menu_r3", "GameText.Key.MenuR3"),
            ("menu_touch_left", "GameText.Key.MenuTouchLeft"),
            ("menu_touch_right", "GameText.Key.MenuTouchRight"),
            ("menu_rotate_up", "GameText.Key.MenuRotateUp"),
            ("menu_rotate_down", "GameText.Key.MenuRotateDown"),
            ("menu_rotate_left", "GameText.Key.MenuRotateLeft"),
            ("menu_rotate_right", "GameText.Key.MenuRotateRight"),
        ]),
    ];

    public static IReadOnlyList<(string Target, string TextKey)> All { get; } = [.. Sections.SelectMany(s => s.Actions)];

    public static IReadOnlyList<string> PageTextKeys { get; } = [.. Sections.Select(s => s.TextKey)];

    public static int PageCount => Sections.Count;

    public static Range Page(int page)
    {
        var start = Sections.Take(page).Sum(s => s.Actions.Count);
        return start..(start + Sections[page].Actions.Count);
    }

    /// <summary>Index into <see cref="All"/> of the action on key row <paramref name="row"/> of <paramref name="page"/>, or null for an empty row.</summary>
    public static int? ActionAt(int page, int row)
    {
        var (start, length) = Page(page).GetOffsetAndLength(All.Count);
        return row >= 0 && row < length ? start + row : null;
    }

    public static InputContext ContextOf(string target) => GameActions.Find(target)?.Context ?? InputContext.Gameplay;

    /// <summary>Keys of all bindings of an action, for display ("LShift+Mouse1 / Q").</summary>
    public static IReadOnlyList<string> KeysOf(IEnumerable<InputBinding> bindings, string target) =>
        bindings.Where(b => b.Target == target).Select(b => b.Keys).ToList();

    /// <summary>Most keys (chords) the binding screen gives one action: "Q / Mouse 3".</summary>
    public const int MaxKeysPerAction = 2;

    /// <summary>Bindings with every binding of <paramref name="target"/> replaced by one to <paramref name="keys"/>; the binding screen shows keys used twice in one context in red (<see cref="SharedKeys"/>).</summary>
    public static List<InputBinding> Rebind(IEnumerable<InputBinding> bindings, string target, string keys) =>
        Rebind(bindings, target, [keys]);

    /// <summary>Bindings with every binding of <paramref name="target"/> replaced by one binding per chord of <paramref name="keys"/> (the first <see cref="MaxKeysPerAction"/>, a chord given twice once), as the defaults have them (lock_on: Q, Mouse3).</summary>
    public static List<InputBinding> Rebind(IEnumerable<InputBinding> bindings, string target, IReadOnlyList<string> keys)
    {
        var context = ContextOf(target);
        var chords = new List<string>();
        foreach (var k in keys)
        {
            if (string.IsNullOrWhiteSpace(k) || chords.Any(c => SameChord(c, k))) continue;
            chords.Add(k);
            if (chords.Count == MaxKeysPerAction) break;
        }
        var replacement = chords.Select(k => new InputBinding(context, target, k)).ToList();
        var result = new List<InputBinding>();
        var inserted = false;
        foreach (var b in bindings)
        {
            if (b.Target == target)
            {
                if (!inserted) result.AddRange(replacement);
                inserted = true;
                continue;
            }
            result.Add(b);
        }
        if (!inserted) result.AddRange(replacement);
        return result;
    }

    /// <summary>Bindings with <paramref name="target"/>'s put back to its default keys (<see cref="DefaultBindings.All"/>).</summary>
    public static List<InputBinding> ResetToDefault(IEnumerable<InputBinding> bindings, string target) =>
        Rebind(bindings, target, KeysOf(DefaultBindings.All, target));

    /// <summary>Whether two chords are the same keys (order and case ignored: "Mouse1+LShift" = "lshift+mouse1").</summary>
    public static bool SameChord(string a, string b) => string.Equals(ChordKey(a), ChordKey(b), StringComparison.Ordinal);

    /// <summary>Chords bound to more than one action of one context, as <see cref="ChordKey"/> values. A key doing a world action and a menu action is not shared: only one acts at a time.</summary>
    public static IReadOnlySet<string> SharedKeys(IEnumerable<InputBinding> bindings, InputContext context = InputContext.Gameplay) =>
        bindings.Where(b => b.Context == context)
            .GroupBy(b => ChordKey(b.Keys))
            .Where(g => g.Select(b => b.Target).Distinct().Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

    public static string ChordKey(string keys) =>
        string.Join("+", new InputBinding(InputContext.Gameplay, "", keys).KeyList.Select(k => k.ToLowerInvariant()).Order(StringComparer.Ordinal));

    /// <summary>Replaces pad button marks in a text ("hold [cross] and press [l3]") with the player's keys ("hold [E] and press [Space]"); an action may be named too ("[interact]"). A button without a key keeps its pad name; unknown marks stay.</summary>
    public static string WithKeys(string text, IReadOnlyList<InputBinding> bindings, Func<string, string> padName,
        IReadOnlyDictionary<string, string>? otherKeys = null) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"\[([a-z0-9_]+)\]", m =>
        {
            var mark = m.Groups[1].Value;
            if (otherKeys is not null && otherKeys.TryGetValue(mark, out var own)) return $"[{DisplayKeys(own)}]"; // e.g. debug keys
            var action = All.Any(a => a.Target == mark) ? GameActions.Find(mark) : GameActions.OfPadTarget(mark);
            if (action is null || !All.Any(a => a.Target == action.Id)) return m.Value;
            var keys = KeysOf(bindings, action.Id);
            return $"[{(keys.Count > 0 ? DisplayKeys(keys[0]) : padName(action.Id == mark ? action.PadTarget : mark))}]";
        });

    /// <summary>Readable key name for the game's menus ("LShift+Mouse1" → "L-Shift + Mouse 1").</summary>
    public static string DisplayKeys(string keys) =>
        string.Join(" + ", keys.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(DisplayKey));

    public static string DisplayKey(string key) => key switch
    {
        "LShift" => "L-Shift",
        "RShift" => "R-Shift",
        "LCtrl" => "L-Ctrl",
        "RCtrl" => "R-Ctrl",
        "LAlt" => "L-Alt",
        "RAlt" => "R-Alt",
        "Escape" => "Esc",
        "WheelUp" => "Wheel Up",
        "WheelDown" => "Wheel Dn",
        _ when key.StartsWith("Mouse", StringComparison.Ordinal) && key.Length == 6 => "Mouse " + key[5],
        _ => key,
    };
}
