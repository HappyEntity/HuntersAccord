using System.Text.Json;
using System.Text.RegularExpressions;
using HuntersAccord.Core.Input;

namespace HuntersAccord.Core.Tests;

/// <summary>
/// Every pad button the game's texts and key guides name, and every mark in the launcher's help texts, shows a key with the
/// default bindings: an action renamed or moved to another context must not leave a pad name on screen.
/// </summary>
public sealed partial class PromptMarksTests
{
    private static readonly string[] TutorialMarks =
    [
        "KG_L_D", "KG_L_LR", "KG_L_U", "KG_L1", "KG_L2", "KG_LS", "KG_R_L", "KG_R_U", "KG_R1", "KG_R2", "KG_R3", "KG_TP_L", "KG_TP_R",
    ];

    [Fact]
    public void Options_in_the_tutorial_shows_the_menu_key()
    {
        Assert.Equal("[Esc]: Open menu", KeyPrompts.ReplaceImages("[OPTIONS]: Open menu", DefaultBindings.All, InputContext.Gameplay));
        var bindings = KeyBindingActions.Rebind(DefaultBindings.All, "menu", "F1");
        Assert.Equal("[F1] : Ouvrir le menu", KeyPrompts.ReplaceImages("[OPTIONS] : Ouvrir le menu", bindings, InputContext.Gameplay));
        // No key for the menu: the game's own text.
        Assert.Equal("[OPTIONS]: Open menu", KeyPrompts.ReplaceImages("[OPTIONS]: Open menu", [], InputContext.Gameplay));
    }

    [Fact]
    public void Every_tutorial_mark_shows_a_world_key()
    {
        foreach (var icon in TutorialMarks)
        {
            var text = KeyPrompts.ReplaceImages($"<img src=\"{icon}\" vspace='-8'>: x", DefaultBindings.All, InputContext.Gameplay);
            Assert.DoesNotContain("KG_", text);
        }
        Assert.DoesNotContain("<?", KeyPrompts.ReplaceImages("<?CancelIcon?>: x", DefaultBindings.All, InputContext.Gameplay));
        foreach (var mark in KeyPrompts.TextMarkTargets.Keys)
            Assert.NotEqual(mark + ": x", KeyPrompts.ReplaceImages(mark + ": x", DefaultBindings.All, InputContext.Gameplay));
    }

    [Fact]
    public void Every_prompt_image_shows_a_key_in_both_contexts()
    {
        foreach (var icon in KeyPrompts.IconTargets.Keys)
        {
            Assert.True(KeyPrompts.Label(DefaultBindings.All, InputContext.Gameplay, icon) is not null, $"world: {icon}");
            // The left stick has no menu action: menus keep its image.
            if (icon != "KG_LS")
                Assert.True(KeyPrompts.Label(DefaultBindings.All, InputContext.Interface, icon) is not null, $"menus: {icon}");
        }
    }

    [Fact]
    public void Every_world_action_has_a_prompt()
    {
        var shown = KeyPrompts.IconTargets.Values.Concat(KeyPrompts.TextMarkTargets.Values).SelectMany(t => t).ToHashSet();
        foreach (var action in GameActions.All.Where(a => a.Context == InputContext.Gameplay))
            Assert.True(shown.Contains(action.PadTarget), $"{action.Id} ({action.PadTarget}) has no prompt image or text mark");
    }

    [Fact]
    public void Every_pad_name_and_action_mark_shows_a_key()
    {
        static string Unbound(string pad) => "UNBOUND " + pad;
        foreach (var action in GameActions.All.Where(a => a.Context == InputContext.Gameplay))
            Assert.DoesNotContain("UNBOUND", KeyBindingActions.WithKeys($"[{action.PadTarget}]", DefaultBindings.All, Unbound));
        foreach (var (target, _) in KeyBindingActions.All)
            Assert.DoesNotContain("UNBOUND", KeyBindingActions.WithKeys($"[{target}]", DefaultBindings.All, Unbound));
    }

    [Fact]
    public void Every_mark_in_the_launcher_help_texts_shows_a_key()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HuntersAccord.slnx"))) dir = dir.Parent;
        var langDir = Path.Combine(dir!.FullName, "src", "HuntersAccord.Launcher", "Resources", "Lang");
        foreach (var file in Directory.EnumerateFiles(langDir, "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            foreach (var entry in doc.RootElement.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String))
                foreach (Match m in Mark().Matches(entry.Value.GetString()!))
                {
                    if (m.Groups[1].Value.StartsWith("debug_", StringComparison.Ordinal)) continue; // the debug keys' own settings
                    var text = KeyBindingActions.WithKeys(m.Value, DefaultBindings.All, pad => "UNBOUND " + pad);
                    Assert.True(text != m.Value && !text.Contains("UNBOUND"), $"{Path.GetFileName(file)} {entry.Name}: {m.Value}");
                }
        }
    }

    [GeneratedRegex(@"\[([a-z0-9_]+)\]")]
    private static partial Regex Mark();
}
