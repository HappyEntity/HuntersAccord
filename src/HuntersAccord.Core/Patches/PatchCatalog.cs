namespace HuntersAccord.Core.Patches;

/// <summary>Where a patch comes from: the two repositories shadPS4's own launcher offers, or Hunters' Accord itself.</summary>
public enum PatchSource
{
    /// <summary>GoldHEN patches (illusionyy/PS-Game-Patch), file Bloodborne-Orbis.xml.</summary>
    GoldHen,

    /// <summary>shadPS4's own patches (shadps4-emu/ps4_cheats), file Bloodborne.xml.</summary>
    ShadPs4,

    /// <summary>Hunters' Accord's own (the mod writes it; nothing to download).</summary>
    HuntersAccord,
}

public sealed record PatchEntry(string Key, PatchSource Source, string Name, string TextKey);

public sealed record PatchSection(string TextKey, IReadOnlyList<PatchEntry> Patches, bool DeveloperOnly = false);

/// <summary>The community patches the player can turn on, grouped as the launcher shows them. All apply when the game starts; patches that change the game for other players are left out, except in the developer-only debug section.</summary>
public static class PatchCatalog
{
    public static IReadOnlyList<PatchSection> Sections { get; } =
    [
        new("PatchSection.HuntersAccord",
        [
            new("fps60_ha", PatchSource.HuntersAccord, "60 FPS (Hunters' Accord)", "Patch.Fps60Ha"),
        ]),
        new("PatchSection.Graphics",
        [
            // Motion blur, chromatic aberration, depth of field and SSAO are game settings now (GameSettings.PatchesMovedToGraphics).
            new("no_dynamic_shadows", PatchSource.ShadPs4, "Disable Dynamic Light Shadows (Perf Increase)", "Patch.NoDynamicShadows"),
        ]),
        new("PatchSection.Performance",
        [
            new("no_vsync", PatchSource.GoldHen, "Disable vsync", "Patch.NoVsync"),
            new("performance", PatchSource.ShadPs4, "Performance Patch (Perf Increase)", "Patch.Performance"),
            new("rgba8f", PatchSource.ShadPs4, "rgba8f color space (Perf Increase)", "Patch.Rgba8f"),
            new("model_lod1", PatchSource.ShadPs4, "Model LOD 1 (Lower model detail)", "Patch.ModelLod1"),
        ]),
        new("PatchSection.Comfort",
        [
            new("change_appearance", PatchSource.ShadPs4, "Restore Change Appearance Feature", "Patch.ChangeAppearance"),
            new("auto_refill", PatchSource.ShadPs4, "Auto-Refill From Storage on Any Area Refresh/Reload", "Patch.AutoRefill"),
            new("text_scale50", PatchSource.ShadPs4, "50% Text scale", "Patch.TextScale50"),
            new("fmod_fix", PatchSource.ShadPs4, "FMOD Crash Fix", "Patch.FmodFix"),
            new("no_facegen", PatchSource.ShadPs4, "Disable Facegen", "Patch.NoFacegen"),
        ]),
        new("PatchSection.Debug",
        [
            new("debug_menu", PatchSource.ShadPs4, "Restore Debug Menu (READ NOTES)", "Patch.DebugMenu"),
            new("debug_camera", PatchSource.GoldHen, "Restore Debug Camera", "Patch.DebugCamera"),
            new("debug_dash", PatchSource.GoldHen, "Restore Debug Dash", "Patch.DebugDash"),
            new("enemy_control", PatchSource.ShadPs4, "Enemy Control", "Patch.EnemyControl"),
            new("no_death", PatchSource.ShadPs4, "Player No Dead (Read note)", "Patch.NoDeath"),
        ], DeveloperOnly: true),
    ];

    /// <summary>
    /// Patches that do not work together although they write different bytes. Hunters' Accord's own 60 FPS writes
    /// elsewhere than the other frame-pacing patches, but sets the frame pacing its own way: one at a time.
    /// </summary>
    public static IReadOnlyList<(string A, string B)> Incompatible { get; } =
    [
        ("fps60_ha", "no_vsync"),
        ("fps60_ha", "performance"),
    ];

    public static bool AreIncompatible(PatchEntry a, PatchEntry b) =>
        Incompatible.Any(p => p.A == a.Key && p.B == b.Key || p.A == b.Key && p.B == a.Key);

    public static IEnumerable<PatchEntry> All => Sections.SelectMany(s => s.Patches);

    public static IReadOnlyList<PatchSection> Visible(bool developerMode) =>
        [.. Sections.Where(s => developerMode || !s.DeveloperOnly)];
}
