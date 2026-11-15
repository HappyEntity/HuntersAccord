using System.Text.Json;
using System.Text.Json.Serialization;
using HuntersAccord.Core.Input;

namespace HuntersAccord.Core.Settings;

/// <summary>Settings changed from inside the game (Hunters' Accord rows in Bloodborne's menus); shared with the launcher.</summary>
public sealed class GameSettings
{
    /// <summary>Keyboard and mouse play; off, the gamepad plays as in the game alone (the mouse camera and key prompts too).</summary>
    public bool KeyboardMouse { get; set; } = true;

    public bool KeyPrompts { get; set; } = true;

    [JsonIgnore]
    public bool ShowKeyPrompts => KeyboardMouse && KeyPrompts;

    /// <summary>Key prompts drawn as key icons; off, as text labels ("[E]").</summary>
    public bool KeyIcons { get; set; } = true;

    [JsonIgnore]
    public bool ShowKeyIcons => ShowKeyPrompts && KeyIcons;

    public bool MouseCamera { get; set; } = true;

    /// <summary>Mouse camera speed multiplier (1 = default).</summary>
    public double MouseSensitivity { get; set; } = 1.0;

    public double MouseSensitivityY { get; set; } = 1.0;

    public bool InvertMouseY { get; set; }

    /// <summary>Moving the mouse right turns the camera left.</summary>
    public bool InvertMouseX { get; set; }

    public bool CameraAutoRotation { get; set; } = true;

    public GraphicsSettings Graphics { get; set; } = new();

    public bool FpsCounter { get; set; }

    /// <summary>
    /// In co-op, the other players' characters played through a short buffer that evens out an uneven link (Auto: only
    /// while the link to that player is uneven; On: always; Off: never, each update shown as it comes).
    /// </summary>
    public PartnerSmoothing PartnerSmoothing { get; set; } = PartnerSmoothing.Auto;

    /// <summary>What a quick item slot's key does: chooses its slot and uses the item, or only chooses it.</summary>
    public QuickItemKeys QuickItemKeys { get; set; } = QuickItemKeys.SelectAndUse;

    /// <summary>Start-time patches whose effect moved into the game's own settings; a player who had one on keeps its effect.</summary>
    public static IReadOnlyDictionary<string, Action<GraphicsSettings>> PatchesMovedToGraphics { get; } =
        new Dictionary<string, Action<GraphicsSettings>>(StringComparer.Ordinal)
        {
            ["no_motion_blur"] = g => g.MotionBlur = false,
            ["no_chromatic_aberration"] = g => g.ChromaticAberration = false,
            ["no_dof"] = g => g.DepthOfField = false,
            ["no_ssao"] = g => g.Ssao = false,
            ["camera_distance"] = g => g.CameraDistance = CameraDistancePatchPercent,
        };

    public const int CameraDistancePatchPercent = 150;

    /// <summary>Turns patches that became game settings (<see cref="PatchesMovedToGraphics"/>) into those settings; true when anything moved.</summary>
    public bool MovePatchesToGraphics()
    {
        var patches = Patches;
        var moved = patches.Where(PatchesMovedToGraphics.ContainsKey).ToList();
        if (moved.Count == 0) return false;
        var graphics = Graphics.Clone();
        foreach (var key in moved) PatchesMovedToGraphics[key](graphics);
        Graphics = graphics;
        EnabledPatches = [.. patches.Except(moved, StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        MotionBlur = true; // the older setting, now carried over
        return true;
    }

    /// <summary>Community patches turned on (<see cref="Patches.PatchCatalog"/> keys), applied when the game starts; null until first set.</summary>
    public List<string>? EnabledPatches { get; set; }

    public bool DeveloperMode { get; set; }

    /// <summary>Older settings (before <see cref="EnabledPatches"/>): read once to carry the choice over.</summary>
    public bool Fps60 { get; set; }

    public bool MotionBlur { get; set; } = true;

    [JsonIgnore]
    public IReadOnlySet<string> Patches
    {
        get
        {
            if (EnabledPatches is not null) return MigrateRemovedPatches(EnabledPatches.ToHashSet(StringComparer.Ordinal));
            var old = new HashSet<string>(StringComparer.Ordinal);
            // A new player's (no choice yet, old or new): No Facegen on (stretched faces on shadPS4 otherwise), the rest
            // as the game is.
            if (!Fps60 && MotionBlur) old.Add("no_facegen");
            if (Fps60) old.Add(OwnFps60);
            if (!MotionBlur) old.Add("no_motion_blur");
            return old;
        }
    }

    private const string OwnFps60 = "fps60_ha";

    /// <summary>The community 60 FPS patch is no longer offered: a player who had it gets Hunters' Accord's own 60 FPS, unless a conflicting patch is on.</summary>
    private static HashSet<string> MigrateRemovedPatches(HashSet<string> patches)
    {
        if (!patches.Remove("fps60")) return patches;
        var blocked = HuntersAccord.Core.Patches.PatchCatalog.Incompatible.Any(p =>
            p.A == OwnFps60 && patches.Contains(p.B) || p.B == OwnFps60 && patches.Contains(p.A));
        if (!blocked) patches.Add(OwnFps60);
        return patches;
    }

    public void SetPatch(string key, bool on)
    {
        var patches = Patches.ToHashSet(StringComparer.Ordinal);
        if (on) patches.Add(key);
        else patches.Remove(key);
        EnabledPatches = [.. patches.Order(StringComparer.Ordinal)];
    }

    /// <summary>Keys of the debug tools (developer patches), as chords like "LCtrl+X".</summary>
    public string DebugToggleKey { get; set; } = "F8";

    public string DebugFlyKey { get; set; } = "V";

    public string DebugUpKey { get; set; } = "Space";

    public string DebugDownKey { get; set; } = "C";

    public string DebugNoclipKey { get; set; } = "X";

    /// <summary>A key that copies the saved game to save-backups (the game saves first). Chords as "LCtrl+X".</summary>
    public string SaveBackupKey { get; set; } = "F6";

    /// <summary>A save backup every so many minutes while in the world (0: off).</summary>
    public int SaveBackupEveryMinutes { get; set; }

    public bool SaveBackupAutoBanner { get; set; } = true;

    /// <summary>How many save backups a player keeps (the newest; older ones are deleted).</summary>
    public int SaveBackupsKept { get; set; } = 10;

    [JsonIgnore]
    public IReadOnlyDictionary<string, string> DebugKeys => new Dictionary<string, string>
    {
        ["debug_toggle"] = DebugToggleKey, ["debug_fly"] = DebugFlyKey, ["debug_up"] = DebugUpKey,
        ["debug_down"] = DebugDownKey, ["debug_noclip"] = DebugNoclipKey,
    };

    public bool SkipLogos { get; set; } = true;


    /// <summary>A lit lamp offers a second action, Rest: the world resets in place (in co-op, for the whole session).</summary>
    public bool LampRest { get; set; } = true;

    /// <summary>In co-op, each player's tag shows the round trip to that player. Set only in the game's Network screen.</summary>
    public bool PingOverPlayers { get; set; } = true;

    /// <summary>Co-op enemy scaling: more HP and damage for each hunter beyond the first; the host's settings count.</summary>
    public bool CoopScaling { get; set; } = true;

    public int ScaleEnemyHp { get; set; } = 30;

    public int ScaleEnemyDamage { get; set; }

    /// <summary>Bosses' HP per extra hunter, percent (the game's own: 50).</summary>
    public int ScaleBossHp { get; set; } = 50;

    public int ScaleBossDamage { get; set; }

    /// <summary>Keyboard & mouse bindings the player changed (world and menus); null means <see cref="DefaultBindings"/>.</summary>
    public List<InputBinding>? Bindings { get; set; }

    /// <summary>Format version of <see cref="Bindings"/>, so bindings saved with older defaults can be upgraded; 0 when absent.</summary>
    public int BindingsFormat { get; set; }

    public const int CurrentBindingsFormat = 3;

    public void SetBindings(List<InputBinding> bindings)
    {
        Bindings = bindings;
        BindingsFormat = CurrentBindingsFormat;
    }

    /// <summary>Bindings from <see cref="Bindings"/> or the defaults. Old pad-button targets are migrated, unknown ones dropped, and actions never saved (or saved with earlier defaults) take today's keys.</summary>
    [JsonIgnore]
    public IReadOnlyList<InputBinding> EffectiveBindings
    {
        get
        {
            if (Bindings is null) return DefaultBindings.All;
            var saved = Bindings
                .Select(b => b with { Target = GameActions.Migrate(b.Context, b.Target) })
                .Where(b => GameActions.Find(b.Context, b.Target) is not null)
                .Distinct()
                .GroupBy(b => b.Target)
                .Where(g => !(BindingsFormat < 2 && SameKeys(g, DefaultBindings.Before.Where(d => d.Target == g.Key))) &&
                            !(BindingsFormat < 3 && SameKeys(g, DefaultBindings.CursorBeforeWasd.Where(d => d.Target == g.Key))))
                .SelectMany(g => g)
                .ToList();
            var savedTargets = saved.Select(b => b.Target).ToHashSet(StringComparer.Ordinal);
            // Bindings saved before the quick item keys keep working: a digit the player gave to something else stays that,
            // its quick item row is empty until the player sets one.
            var savedKeys = saved.Select(b => (b.Context, KeyBindingActions.ChordKey(b.Keys))).ToHashSet();
            return [.. saved, .. DefaultBindings.All.Where(b => !savedTargets.Contains(b.Target)
                && !(GameActions.IsQuickItem(b.Target) && savedKeys.Contains((b.Context, KeyBindingActions.ChordKey(b.Keys)))))];
        }
    }

    private static bool SameKeys(IEnumerable<InputBinding> a, IEnumerable<InputBinding> b) =>
        a.Select(x => KeyBindingActions.ChordKey(x.Keys)).ToHashSet(StringComparer.Ordinal)
            .SetEquals(b.Select(x => KeyBindingActions.ChordKey(x.Keys)));

    public static string FilePath => Path.Combine(AppPaths.DataRoot, "game-settings.json");

    public static GameSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var settings = JsonSerializer.Deserialize(File.ReadAllText(FilePath), GameSettingsJsonContext.Default.GameSettings)
                               ?? new GameSettings();
                settings.Graphics ??= new GraphicsSettings();
                settings.MovePatchesToGraphics(); // saved with the next change
                return settings;
            }
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
        }
        return new GameSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(AppPaths.DataRoot);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, GameSettingsJsonContext.Default.GameSettings));
        File.Move(tmp, FilePath, overwrite: true);
    }
}

/// <summary>The values of <see cref="GameSettings.PartnerSmoothing"/>.</summary>
public enum PartnerSmoothing
{
    Auto,
    On,
    Off,
}

/// <summary>The values of <see cref="GameSettings.QuickItemKeys"/>.</summary>
public enum QuickItemKeys
{
    SelectAndUse,
    SelectOnly,
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(GameSettings))]
internal sealed partial class GameSettingsJsonContext : JsonSerializerContext;
