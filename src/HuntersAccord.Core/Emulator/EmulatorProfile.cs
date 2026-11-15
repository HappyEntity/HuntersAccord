using System.Text.Json;
using System.Text.Json.Nodes;
using HuntersAccord.Core.Settings;

namespace HuntersAccord.Core.Emulator;

/// <summary>
/// Hunters' Accord's own shadPS4 profile. shadPS4 uses <c>&lt;working dir&gt;/user</c> as its data folder when it exists (portable mode),
/// so the emulator starts from <see cref="Root"/> and never touches the player's regular shadPS4 settings and saves.
/// </summary>
public sealed class EmulatorProfile(string root)
{
    private static readonly int[] UserIds = [1000, 1001, 1002, 1003];

    private static EmulatorProfile? _default;

    /// <summary>Player 1's profile in the data folder (<see cref="AppPaths.DataRoot"/>); a new object when the folder moved.</summary>
    public static EmulatorProfile Default
    {
        get
        {
            var root = RootIn(AppPaths.DataRoot, 1);
            var current = _default;
            if (current is not null && AppPaths.SamePath(current.Root, root)) return current;
            return _default = new EmulatorProfile(root);
        }
    }

    /// <summary>A local player's profile: player 1 is <see cref="Default"/>; others have their own settings and saves in <c>profile-N</c>.</summary>
    public static EmulatorProfile ForPlayer(int player) =>
        player <= 1 ? Default : new(RootIn(AppPaths.DataRoot, player));

    /// <summary>The folder of a player's profile in a data folder: profile (player 1), profile-N.</summary>
    public static string RootIn(string dataRoot, int player) =>
        Path.Combine(dataRoot, player <= 1 ? "profile" : $"profile-{player}");

    /// <summary>Creates another player's profile once, as a copy of <paramref name="source"/> (settings, key bindings, saves).</summary>
    public void EnsureCopiedFrom(EmulatorProfile source)
    {
        if (!Exists && source.Exists)
        {
            try
            {
                CopyDirectory(source.UserDir, UserDir, skip: ["log", "shader", "cache", "captures", "screenshots", "temp"]);
            }
            catch
            {
                Directory.Delete(UserDir, recursive: true); // a half copy would be kept as the profile next time
                throw;
            }
        }
        EnsureUserHomes();
    }

    private static void CopyDirectory(string from, string to, string[] skip)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from))
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (var dir in Directory.EnumerateDirectories(from))
            if (!skip.Contains(Path.GetFileName(dir), StringComparer.OrdinalIgnoreCase))
                CopyDirectory(dir, Path.Combine(to, Path.GetFileName(dir)), []);
    }

    public string Root { get; } = root;

    public string UserDir => Path.Combine(Root, "user");

    public string HomeDir => Path.Combine(UserDir, "home");

    public string ConfigFile => Path.Combine(UserDir, "config.json");

    public string LogFile => Path.Combine(UserDir, "log", "shad_log.txt");

    /// <summary>The last run's shadPS4 log as shad_log.old.txt (shadPS4 starts its log over each run).</summary>
    public void KeepLastLog()
    {
        try
        {
            if (File.Exists(LogFile)) File.Copy(LogFile, Path.ChangeExtension(LogFile, ".old.txt"), overwrite: true);
        }
        catch (IOException)
        {
            // The log is not worth failing the start over.
        }
    }

    public bool Exists => Directory.Exists(UserDir);

    /// <summary>Creates the profile on first use, seeding graphics config and key bindings from regular shadPS4 (not saves).</summary>
    public void EnsureCreated(string? seedFromUserDir)
    {
        if (!Exists)
        {
            Directory.CreateDirectory(UserDir);
            if (seedFromUserDir is not null && Directory.Exists(seedFromUserDir))
                Seed(seedFromUserDir);
        }
        EnsureUserHomes();
    }

    private void Seed(string seedFromUserDir)
    {
        var config = Path.Combine(seedFromUserDir, "config.json");
        if (File.Exists(config)) WriteIsolatedConfig(File.ReadAllText(config));

        var inputs = Path.Combine(seedFromUserDir, "input_config");
        if (Directory.Exists(inputs))
        {
            var target = Path.Combine(UserDir, "input_config");
            Directory.CreateDirectory(target);
            foreach (var file in Directory.EnumerateFiles(inputs, "*.ini"))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }
    }

    /// <summary>Copies the config but clears paths that would make the profile share saves with regular shadPS4.</summary>
    private void WriteIsolatedConfig(string json)
    {
        try
        {
            var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true });
            if (root?["General"] is JsonObject general)
                general["home_dir"] = "";
            File.WriteAllText(ConfigFile, root!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (JsonException)
        {
            // Unreadable config: let shadPS4 create a default one.
        }
    }

    /// <summary>Sets a boolean in the section of config.json that already has it (sections differ between versions), else adds it to <paramref name="section"/>.</summary>
    public void SetConfigValue(string section, string key, bool value) => EditConfig(root => SetConfig(root, section, key, value));

    /// <summary>Turns on shadPS4's static patching of the game's code: the 0.18 WindowsGuestRedZoneProtection key if present, else General.redzone_patches (0.19 to 0.21).</summary>
    public void UseStaticPatching() => EditConfig(root =>
    {
        const string Mode = "windows_guest_red_zone_protection_mode", Static = "StaticPatching";
        if (root["WindowsGuestRedZoneProtection"] is JsonObject legacy && legacy[Mode] is JsonValue current
            && current.GetValueKind() == JsonValueKind.String)
        {
            legacy[Mode] = Static;
            return;
        }
        SetConfig(root, "General", "redzone_patches", true);
    });

    /// <summary>With the mod the game's NP is answered by Hunters' Accord, so shadPS4's own online (shadNet) stays off, else its rooms and signaling would run beside the mod's.</summary>
    public void KeepShadNetOff() => EditConfig(root => SetConfig(root, "Network", "shad_net_enabled", false));

    /// <summary>
    /// With the mod the trophies' toasts are Hunters' Accord's own, so shadPS4's trophy popup (General.trophy_popup_disabled)
    /// stays off: its ImGui popup, refilled from the game's trophy thread while shadPS4:Main draws it, asserted
    /// (imgui.h:2358, i &lt; Size) on 0.20 when the game awarded a trophy again.
    /// </summary>
    public void KeepTrophyPopupOff() => EditConfig(root => SetConfig(root, "General", "trophy_popup_disabled", true));

    public void SetConsoleLanguage(int languageId)
    {
        JsonNode root;
        try
        {
            root = File.Exists(ConfigFile)
                ? JsonNode.Parse(File.ReadAllText(ConfigFile), documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true }) ?? new JsonObject()
                : new JsonObject();
        }
        catch (JsonException)
        {
            root = new JsonObject();
        }

        if (root["General"] is not JsonObject general)
            root["General"] = general = new JsonObject();
        if (general["console_language"]?.GetValueKind() == JsonValueKind.Number && (int)general["console_language"]! == languageId)
            return;

        general["console_language"] = languageId;
        Directory.CreateDirectory(UserDir);
        File.WriteAllText(ConfigFile, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Writes the launcher's picture settings to config.json; <paramref name="windowed"/> forces a window (two instances side by side).</summary>
    public void ApplyDisplay(DisplaySettings display, bool windowed = false)
    {
        var mode = windowed ? ScreenMode.Windowed : display.ScreenMode;
        EditConfig(root =>
        {
            SetConfig(root, "GPU", "full_screen", mode != ScreenMode.Windowed);
            SetConfig(root, "GPU", "full_screen_mode", mode switch
            {
                ScreenMode.Windowed => "Windowed",
                ScreenMode.Borderless => "Fullscreen (Borderless)",
                _ => "Fullscreen", // shadPS4 takes the monitor's current mode for exclusive fullscreen
            });
            SetConfig(root, "GPU", "window_width", Math.Max(640, display.WindowWidth));
            SetConfig(root, "GPU", "window_height", Math.Max(360, display.WindowHeight));
            SetConfig(root, "GPU", "present_mode", display.VSync switch
            {
                VSyncMode.Fifo => "Fifo",
                VSyncMode.Off => "Immediate",
                _ => "Mailbox",
            });
            SetConfig(root, "GPU", "fsr_enabled", display.Fsr);
            SetConfig(root, "GPU", "rcas_attenuation", Math.Clamp(display.RcasAttenuation, 0, DisplaySettings.MaxRcasAttenuation));
            SetConfig(root, "General", "show_fps_counter", display.ShowFps);
            SetConfig(root, "Vulkan", "pipeline_cache_enabled", display.ShaderCache);
            // A frame larger than 1080p needs a larger render-target heap, which the mod grows inside the game's direct
            // memory: shadPS4 gives the game that much more (none at 1080p; also undoes a value seeded from another config).
            SetConfig(root, "General", "extra_dmem_in_mbytes", display.Resolution.ExtraDirectMemoryMiB);
        });
    }

    /// <summary>Loads config.json (a missing or unreadable one starts empty: shadPS4 fills in its defaults), edits it and saves it when changed.</summary>
    private void EditConfig(Action<JsonObject> edit)
    {
        JsonObject root;
        try
        {
            root = File.Exists(ConfigFile)
                ? JsonNode.Parse(File.ReadAllText(ConfigFile), documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true }) as JsonObject ?? []
                : [];
        }
        catch (JsonException)
        {
            root = [];
        }
        var before = root.ToJsonString();
        edit(root);
        if (root.ToJsonString() == before) return;
        Directory.CreateDirectory(UserDir);
        File.WriteAllText(ConfigFile, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void SetConfig(JsonObject root, string section, string key, JsonNode value)
    {
        var owner = root.Select(p => p.Value).OfType<JsonObject>().FirstOrDefault(s => s.ContainsKey(key));
        if (owner is null)
        {
            if (root[section] is not JsonObject target) root[section] = target = [];
            owner = target;
        }
        owner[key] = value;
    }

    /// <summary>shadPS4's pipeline cache of a game: <c>cache/&lt;TITLE_ID&gt;</c>, or one archive <c>cache/&lt;TITLE_ID&gt;.zip</c>.</summary>
    private (string Dir, string Archive) ShaderCachePaths(string titleId) =>
        (Path.Combine(UserDir, "cache", titleId), Path.Combine(UserDir, "cache", titleId + ".zip"));

    public long ShaderCacheSize(string titleId)
    {
        var (dir, archive) = ShaderCachePaths(titleId);
        var size = File.Exists(archive) ? new FileInfo(archive).Length : 0;
        if (Directory.Exists(dir))
            size += new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        return size;
    }

    /// <summary>Deletes the game's shader cache (only with the game closed: shadPS4 keeps it open).</summary>
    public void ClearShaderCache(string titleId)
    {
        var (dir, archive) = ShaderCachePaths(titleId);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        if (File.Exists(archive)) File.Delete(archive);
    }

    /// <summary>shadPS4 ignores a shader cache made for another GPU, driver or build; it is deleted before a start in that case so it is built again.
    /// Returns why it was deleted, also kept in <see cref="LastShaderCacheReset"/>.</summary>
    public ShaderCacheReset KeepShaderCacheUsable(string titleId, string emulatorExe)
    {
        var (dir, archive) = ShaderCachePaths(titleId);
        var stamp = Path.Combine(UserDir, "cache", titleId + ".ha");
        var exe = new FileInfo(emulatorExe);
        var build = $"{exe.FullName}|{(exe.Exists ? exe.Length : 0)}|{(exe.Exists ? exe.LastWriteTimeUtc.Ticks : 0)}";
        var reason = ShaderCacheReset.None;
        try
        {
            if (Directory.Exists(dir) || File.Exists(archive))
            {
                if (LastRunIgnoredShaderCache()) reason = ShaderCacheReset.GpuOrDriverChanged;
                else if (File.Exists(stamp) && File.ReadLines(stamp).FirstOrDefault() is { } made && made != build)
                    reason = ShaderCacheReset.EmulatorChanged;
                if (reason != ShaderCacheReset.None) ClearShaderCache(titleId);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(stamp)!);
            File.WriteAllText(stamp, reason == ShaderCacheReset.None ? build : $"{build}\ncleared {DateTime.Now:yyyy-MM-dd HH:mm}: {reason}");
        }
        catch (IOException)
        {
            // A cache in use (the game still running) or a log being written: kept as it is.
        }
        catch (UnauthorizedAccessException)
        {
        }
        LastShaderCacheReset = reason;
        return reason;
    }

    public ShaderCacheReset LastShaderCacheReset { get; private set; }

    /// <summary>The last run's log says shadPS4 ignored the shader cache (its warm-up comes early: the log's start is enough).</summary>
    private bool LastRunIgnoredShaderCache()
    {
        if (!File.Exists(LogFile)) return false;
        using var stream = new FileStream(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        for (var i = 0; i < 50_000 && reader.ReadLine() is { } line; i++)
        {
            if (line.Contains("Ignoring the cache", StringComparison.Ordinal)) return true;
            if (line.Contains("WarmUp: Preloaded", StringComparison.Ordinal)) return false;
        }
        return false;
    }

    /// <summary>Pre-creates the per-user folders: shadPS4 shows a "Save Migration" prompt whenever it creates <c>home/1000</c> itself.</summary>
    private void EnsureUserHomes()
    {
        foreach (var id in UserIds)
        {
            var dir = Path.Combine(HomeDir, id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(Path.Combine(dir, "savedata"));
            Directory.CreateDirectory(Path.Combine(dir, "trophy"));
            Directory.CreateDirectory(Path.Combine(dir, "inputs"));
        }
    }
}

public enum ShaderCacheReset
{
    None,

    /// <summary>The last run's shadPS4 ignored the cache: another GPU or driver.</summary>
    GpuOrDriverChanged,

    EmulatorChanged,
}
