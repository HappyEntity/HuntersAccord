using HuntersAccord.Core.Input;

namespace HuntersAccord.Core.Tests;

public sealed class KeyPromptsTests
{
    [Fact]
    public void Menu_prompts_show_the_menu_keys()
    {
        Assert.Equal("[E]", KeyPrompts.Label(DefaultBindings.All, InputContext.Interface, "KG_Cancel"));
        Assert.Equal("[Q]", KeyPrompts.Label(DefaultBindings.All, InputContext.Interface, "KG_OK"));
        Assert.Equal("[Up/Down]", KeyPrompts.Label(DefaultBindings.All, InputContext.Interface, "KG_L_UD"));
        Assert.Equal("[Up/Left/Down/Right]", KeyPrompts.Label(DefaultBindings.All, InputContext.Interface, "KG_L_UDLR"));
    }

    [Fact]
    public void Menu_right_stick_turns_with_left_and_right_keys()
    {
        // Character creation's "Rotate": a short label in a full key guide; in the world the camera keeps all four.
        Assert.Equal("[J/L]", KeyPrompts.Label(DefaultBindings.All, InputContext.Interface, "KG_RS"));
        Assert.Equal("[I/J/K/L]", KeyPrompts.Label(DefaultBindings.All, InputContext.Gameplay, "KG_RS"));
    }

    [Fact]
    public void World_prompts_follow_rebound_gameplay_keys()
    {
        var bindings = KeyBindingActions.Rebind(DefaultBindings.All, "interact", "F");
        Assert.Equal("[F]", KeyPrompts.Label(bindings, InputContext.Gameplay, "KG_Cancel"));
    }

    [Fact]
    public void Unknown_images_and_unbound_buttons_keep_the_image()
    {
        Assert.Null(KeyPrompts.Label(DefaultBindings.All, InputContext.Interface, "FE_key_black"));
        Assert.Null(KeyPrompts.Label([], InputContext.Interface, "KG_L3"));
        Assert.Equal("[H]", KeyPrompts.Label(DefaultBindings.All, InputContext.Interface, "KG_R3"));
    }

    [Fact]
    public void Tutorial_texts_get_gameplay_keys_instead_of_inline_images()
    {
        var text = KeyPrompts.ReplaceImages(
            "<img src=\"KG_R1\" vspace='-8'>: Attack\n<img src=\"KG_LS\" vspace='-8'>+<?CancelIcon?>: Roll",
            DefaultBindings.All, InputContext.Gameplay);
        Assert.Equal("[Mouse 1]: Attack\n[W/A/S/D]+[Space]: Roll", text);
        Assert.Equal("No images", KeyPrompts.ReplaceImages("No images", DefaultBindings.All, InputContext.Gameplay));
    }

    [Fact]
    public void A_plain_mouse_button_wins_then_a_key_then_a_mouse_chord()
    {
        var bindings = new List<InputBinding>
        {
            new(InputContext.Gameplay, "lock_on", "Q"), new(InputContext.Gameplay, "lock_on", "Mouse3"),
            new(InputContext.Gameplay, "strong_attack", "LShift+Mouse1"), new(InputContext.Gameplay, "strong_attack", "G"),
            new(InputContext.Gameplay, "transform", "LShift+Mouse2"),
        };
        Assert.Equal("[Mouse 3]", KeyPrompts.Label(bindings, InputContext.Gameplay, "KG_R3"));
        Assert.Equal("[G]", KeyPrompts.Label(bindings, InputContext.Gameplay, "KG_R2"));
        Assert.Equal($"[{KeyBindingActions.DisplayKeys("LShift+Mouse2")}]", KeyPrompts.Label(bindings, InputContext.Gameplay, "KG_L1"));
    }
}
