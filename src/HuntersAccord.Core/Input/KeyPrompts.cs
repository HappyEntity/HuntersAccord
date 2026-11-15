namespace HuntersAccord.Core.Input;

/// <summary>
/// Labels for Bloodborne's button prompts: the game draws a prompt as an image named after the pad button
/// (<c>KG_OK</c>, <c>KG_Cancel</c>, <c>KG_L_UD</c>, ...); Hunters' Accord shows the keys bound to that button instead.
/// </summary>
public static partial class KeyPrompts
{
    public static IReadOnlyDictionary<string, string[]> IconTargets { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        // Named after the Japanese layout (confirm = circle): KG_OK shows the circle, KG_Cancel the cross.
        ["KG_OK"] = ["circle"],
        ["KG_Cancel"] = ["cross"],
        ["KG_R_U"] = ["triangle"],
        ["KG_R_L"] = ["square"],
        ["KG_L_U"] = ["dpad_up"],
        ["KG_L_D"] = ["dpad_down"],
        ["KG_L_L"] = ["dpad_left"],
        ["KG_L_R"] = ["dpad_right"],
        ["KG_L_UD"] = ["dpad_up", "dpad_down"],
        ["KG_L_LR"] = ["dpad_left", "dpad_right"],
        ["KG_L_UDLR"] = ["dpad_up", "dpad_left", "dpad_down", "dpad_right"],
        ["KG_L1"] = ["l1"],
        ["KG_R1"] = ["r1"],
        ["KG_L2"] = ["l2"],
        ["KG_R2"] = ["r2"],
        ["KG_L3"] = ["l3"],
        ["KG_R3"] = ["r3"],
        ["KG_TP_L"] = ["touchpad_left"],
        ["KG_TP_R"] = ["touchpad_right"],
        ["KG_LS"] = ["move_forward", "move_left", "move_back", "move_right"],
        ["KG_RS"] = ["camera_up", "camera_left", "camera_down", "camera_right"],
    };

    /// <summary>Menus' own reading of a prompt image: the right stick turns a model (character creation), so its keys are left and right only.</summary>
    private static readonly Dictionary<string, string[]> InterfaceIconTargets = new(StringComparer.Ordinal)
    {
        ["KG_RS"] = ["camera_left", "camera_right"],
    };

    /// <summary>Buttons the game's texts name in plain text instead of an image: the tutorial "[OPTIONS]: Open menu" (menu FMG 2, id 1004).</summary>
    public static IReadOnlyDictionary<string, string[]> TextMarkTargets { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["[OPTIONS]"] = ["options"],
    };

    /// <summary>Keys for a prompt image in a context ("[Esc]", "[W/S]"), or null to keep the image (unknown image, or a button with no key).</summary>
    public static string? Label(IEnumerable<InputBinding> bindings, InputContext context, string icon)
    {
        return TargetsOf(context, icon) is { } targets ? Label(bindings, context, targets) : null;
    }

    private static string? Label(IEnumerable<InputBinding> bindings, InputContext context, string[] targets)
    {
        var keys = PromptChords(bindings, context, targets).Select(KeyBindingActions.DisplayKeys).Distinct().ToList();
        return keys.Count == 0 ? null : $"[{string.Join("/", keys)}]";
    }

    private static List<string> PromptChords(IEnumerable<InputBinding> bindings, InputContext context, string[] targets)
    {
        var list = bindings as IReadOnlyList<InputBinding> ?? bindings.ToList();
        return targets
            // A gameplay binding names its action: the prompt's button is the one that action is on the gamepad.
            .Select(t => Preferred(list.Where(b => b.Context == context && GameActions.PadTargetOf(b) == t)))
            .OfType<InputBinding>()
            .Select(b => b.Keys)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string[]? TargetsOf(InputContext context, string icon) =>
        context == InputContext.Interface && InterfaceIconTargets.TryGetValue(icon, out var menu) ? menu
        : IconTargets.GetValueOrDefault(icon);

    /// <summary>The prompt image as key icons (<see cref="KeyIcons"/>), a chord's keys joined by "+", a direction set side by side or as its group icon. Null (show the text label) when the image is not a button, has no key, or an icon is missing from <paramref name="icons"/>.</summary>
    public static string? IconMarkup(IEnumerable<InputBinding> bindings, InputContext context, string icon,
        IReadOnlySet<string>? icons, bool mouseCamera = true) =>
        icons is null || TargetsOf(context, icon) is not { } targets ? null : IconMarkup(bindings, context, targets, icons, mouseCamera);

    private static string? IconMarkup(IEnumerable<InputBinding> bindings, InputContext context, string[] targets,
        IReadOnlySet<string> icons, bool mouseCamera)
    {
        var chords = PromptChords(bindings, context, targets);
        return chords.Count == 0 ? null : ChordIcons(targets, chords, context, icons, mouseCamera);
    }

    private static string? ChordIcons(IReadOnlyList<string> targets, List<string> chords, InputContext context,
        IReadOnlySet<string> icons, bool mouseCamera)
    {
        if (KeyIcons.GroupOf(targets, chords, context, mouseCamera) is { } group && icons.Contains(group))
            return KeyIcons.Image(group);
        var parts = new List<string>();
        foreach (var chord in chords)
        {
            var images = new List<string>();
            foreach (var key in new InputBinding(context, "", chord).KeyList)
            {
                if (KeyIcons.IconOfKey(key) is not { } name || !icons.Contains(name)) return null;
                images.Add(KeyIcons.Image(name));
            }
            parts.Add(string.Join(KeyIcons.ChordJoin, images));
        }
        return string.Join(chords.Any(c => c.Contains('+')) ? "/" : "", parts);
    }

    /// <summary>The binding a prompt shows when a button has several: a plain mouse button first, then a keyboard key, a mouse chord last.</summary>
    private static InputBinding? Preferred(IEnumerable<InputBinding> bindings)
    {
        var list = bindings.ToList();
        return list.FirstOrDefault(b => b.KeyList is [var key] && IsMouse(key))
               ?? list.FirstOrDefault(b => !b.KeyList.Any(IsMouse))
               ?? list.FirstOrDefault();
    }

    private static bool IsMouse(string key) =>
        key.StartsWith("Mouse", StringComparison.Ordinal) || key.StartsWith("Wheel", StringComparison.Ordinal);

    /// <summary>Game texts with pad images inline (<c>&lt;img src="KG_R1" vspace='-8'&gt;: Attack</c>) with the images replaced by key labels; <c>&lt;?CancelIcon?&gt;</c> and plain-text marks ("[OPTIONS]") too. Unchanged when nothing needs replacing.</summary>
    /// <param name="icons">
    /// The key icons the game files were built with (see <see cref="IconMarkup(IEnumerable{InputBinding}, InputContext, string, IReadOnlySet{string}?, bool)"/>):
    /// a button whose keys all have one shows them as images; null, or an icon missing: the text label.
    /// </param>
    /// <param name="mouseCamera">The mouse turns the camera (the right stick's prompt is the mouse then).</param>
    public static string ReplaceImages(string text, IEnumerable<InputBinding> bindings, InputContext context,
        IReadOnlySet<string>? icons = null, bool mouseCamera = true)
    {
        if (!HasButtons(text)) return text;
        var list = bindings as IReadOnlyList<InputBinding> ?? bindings.ToList();
        string? Shown(string[]? targets) => targets is null ? null
            : (icons is null ? null : IconMarkup(list, context, targets, icons, mouseCamera)) ?? Label(list, context, targets);
        text = InlineImage().Replace(text, m => Shown(TargetsOf(context, m.Groups[1].Value)) ?? m.Value);
        foreach (var (mark, targets) in TextMarkTargets)
            if (text.Contains(mark, StringComparison.Ordinal) && Shown(targets) is { } label)
                text = text.Replace(mark, label, StringComparison.Ordinal);
        return text.Replace(CancelIconTag, Shown(TargetsOf(context, "KG_OK")) ?? CancelIconTag, StringComparison.Ordinal);
    }

    /// <summary>Hunters' Accord's own texts name keys by marks resolved when shown, so a rebind shows at once: <c>{key:menu_confirm}</c> an action's key, <c>{key:Delete}</c> a fixed key, <c>{keys:menu_left,menu_right}</c> several as one set. A mark becomes key icons when <paramref name="icons"/> has them all, else the text label; unknown marks stay.</summary>
    public static string ResolveKeyMarks(string text, IEnumerable<InputBinding> bindings, IReadOnlySet<string>? icons,
        bool mouseCamera = true)
    {
        if (!text.Contains("{key", StringComparison.Ordinal)) return text;
        var list = bindings as IReadOnlyList<InputBinding> ?? bindings.ToList();
        return KeyMark().Replace(text, m =>
        {
            var names = m.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var chords = new List<string>();
            foreach (var name in names)
            {
                if (GameActions.Find(name) is { } action)
                {
                    if (Preferred(list.Where(b => b.Target == action.Id && b.Context == action.Context)) is { } binding) chords.Add(binding.Keys);
                }
                else if (KeyIcons.IconOfKey(name) is not null) chords.Add(name);
                else return m.Value;
            }
            chords = chords.Distinct(StringComparer.Ordinal).ToList();
            if (chords.Count == 0) return m.Value;
            return (icons is null ? null : ChordIcons(names, chords, InputContext.Interface, icons, mouseCamera))
                   ?? $"[{string.Join("/", chords.Select(KeyBindingActions.DisplayKeys))}]";
        });
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\{(keys?):([A-Za-z0-9_,]+)\}")]
    private static partial System.Text.RegularExpressions.Regex KeyMark();

    public static bool HasButtons(string text) =>
        text.Contains("KG_", StringComparison.Ordinal) || text.Contains(CancelIconTag, StringComparison.Ordinal)
        || TextMarkTargets.Keys.Any(mark => text.Contains(mark, StringComparison.Ordinal));

    /// <summary>Tag the game expands to the circle icon (Japanese naming: the circle is "OK", see <see cref="IconTargets"/>).</summary>
    private const string CancelIconTag = "<?CancelIcon?>";

    [System.Text.RegularExpressions.GeneratedRegex("<img src=[\"'](KG_[A-Za-z0-9_]+)[\"'][^>]*>")]
    private static partial System.Text.RegularExpressions.Regex InlineImage();
}
