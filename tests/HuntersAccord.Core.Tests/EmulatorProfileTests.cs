using System.Text.Json.Nodes;
using HuntersAccord.Core.Emulator;
using HuntersAccord.Core.Settings;

namespace HuntersAccord.Core.Tests;

public sealed class EmulatorProfileTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("ha-profile-").FullName;

    [Fact]
    public void Seeding_copies_settings_but_isolates_saves()
    {
        var seed = Path.Combine(_temp, "shadPS4");
        Directory.CreateDirectory(Path.Combine(seed, "input_config"));
        File.WriteAllText(Path.Combine(seed, "config.json"),
            """{ "General": { "home_dir": "D:\\shared-saves", "show_fps_counter": true } }""");
        File.WriteAllText(Path.Combine(seed, "input_config", "default.ini"), "cross = n");

        var profile = new EmulatorProfile(Path.Combine(_temp, "profile"));
        profile.EnsureCreated(seed);

        var config = JsonNode.Parse(File.ReadAllText(profile.ConfigFile))!;
        Assert.Equal("", (string?)config["General"]!["home_dir"]);
        Assert.True((bool)config["General"]!["show_fps_counter"]!);
        Assert.True(File.Exists(Path.Combine(profile.UserDir, "input_config", "default.ini")));
    }

    [Fact]
    public void Precreates_user_homes_to_avoid_migration_prompt()
    {
        var profile = new EmulatorProfile(Path.Combine(_temp, "profile"));
        profile.EnsureCreated(seedFromUserDir: null);

        foreach (var id in new[] { "1000", "1001", "1002", "1003" })
            Assert.True(Directory.Exists(Path.Combine(profile.HomeDir, id, "savedata")));
    }

    [Fact]
    public void Display_settings_go_where_shadps4_keeps_them()
    {
        var profile = new EmulatorProfile(Path.Combine(_temp, "profile"));
        Directory.CreateDirectory(profile.UserDir);
        File.WriteAllText(profile.ConfigFile,
            """{ "GPU": { "full_screen": false, "full_screen_mode": "Windowed", "null_gpu": false }, "Vulkan": { "gpu_id": -1 } }""");

        profile.ApplyDisplay(new DisplaySettings
        {
            ScreenMode = ScreenMode.Borderless, WindowWidth = 1920, WindowHeight = 1080, VSync = VSyncMode.Off, Fsr = true,
            SharpnessPercent = 100, ShowFps = true,
        });

        var config = JsonNode.Parse(File.ReadAllText(profile.ConfigFile))!;
        var gpu = config["GPU"]!;
        Assert.True((bool)gpu["full_screen"]!);
        Assert.Equal("Fullscreen (Borderless)", (string?)gpu["full_screen_mode"]);
        Assert.Equal(1920, (int)gpu["window_width"]!);
        Assert.Equal("Immediate", (string?)gpu["present_mode"]);
        Assert.True((bool)gpu["fsr_enabled"]!);
        Assert.Equal(0, (int)gpu["rcas_attenuation"]!);
        Assert.False((bool)gpu["null_gpu"]!);
        Assert.True((bool)config["General"]!["show_fps_counter"]!);
        Assert.True((bool)config["Vulkan"]!["pipeline_cache_enabled"]!);
        Assert.Equal(-1, (int)config["Vulkan"]!["gpu_id"]!);
    }

    [Theory]
    [InlineData("""{ "General": { "shad_net_enabled": true, "show_fps_counter": true } }""", "General")] // 0.19
    [InlineData("""{ "Network": { "shad_net_enabled": true, "p2p_port": 3658 } }""", "Network")]        // 0.20
    [InlineData("{}", "Network")]                                                                          // none yet
    public void ShadNet_is_kept_off_where_the_config_has_it(string json, string section)
    {
        var profile = new EmulatorProfile(Path.Combine(_temp, "profile"));
        Directory.CreateDirectory(profile.UserDir);
        File.WriteAllText(profile.ConfigFile, json);

        profile.KeepShadNetOff();

        var config = JsonNode.Parse(File.ReadAllText(profile.ConfigFile))!.AsObject();
        Assert.False((bool)config[section]!["shad_net_enabled"]!);
        Assert.Single(config, s => s.Value is JsonObject o && o.ContainsKey("shad_net_enabled"));
    }

    [Theory]
    [InlineData("""{ "General": { "trophy_popup_disabled": false, "show_fps_counter": true } }""")] // 0.19, 0.20
    [InlineData("{}")]                                                                               // none yet
    public void The_trophy_popup_is_kept_off(string json)
    {
        var profile = new EmulatorProfile(Path.Combine(_temp, "profile"));
        Directory.CreateDirectory(profile.UserDir);
        File.WriteAllText(profile.ConfigFile, json);

        profile.KeepTrophyPopupOff();

        var config = JsonNode.Parse(File.ReadAllText(profile.ConfigFile))!.AsObject();
        Assert.True((bool)config["General"]!["trophy_popup_disabled"]!);
        Assert.Single(config, s => s.Value is JsonObject o && o.ContainsKey("trophy_popup_disabled"));
    }

    [Theory]
    [InlineData(null)]                                                                         // no config.json yet
    [InlineData("""{ "General": { "console_language": 1 }, "GPU": { "full_screen": true } }""")] // only the launcher's settings
    public void A_fresh_profile_starts_with_the_per_game_input_config_and_static_patching(string? json)
    {
        var profile = new EmulatorProfile(Path.Combine(_temp, "profile"));
        profile.EnsureCreated(seedFromUserDir: null);
        if (json is not null) File.WriteAllText(profile.ConfigFile, json);

        EmulatorSession.PrepareInput(profile, modLayerDir: null);
        profile.UseStaticPatching();

        var config = JsonNode.Parse(File.ReadAllText(profile.ConfigFile))!;
        Assert.False((bool)config["Input"]!["use_unified_input_config"]!);
        Assert.True((bool)config["General"]!["redzone_patches"]!);
        if (json is not null) Assert.Equal(1, (int)config["General"]!["console_language"]!);
        Assert.True(File.Exists(Path.Combine(profile.UserDir, "input_config", "CUSA03173.ini")));
        Assert.DoesNotContain(File.ReadAllLines(Path.Combine(profile.UserDir, "input_config", "global.ini")), l => l.TrimEnd().EndsWith('='));
    }

    [Theory]
    [InlineData("""{ "General": { "use_unified_input_config": true, "redzone_patches": false } }""", "General")] // where a version keeps it
    [InlineData("""{ "Input": { "use_unified_input_config": true }, "General": { "redzone_patches": false } }""", "Input")] // 0.20
    public void Input_and_patching_settings_change_where_the_config_has_them(string json, string section)
    {
        var profile = new EmulatorProfile(Path.Combine(_temp, "profile"));
        Directory.CreateDirectory(profile.UserDir);
        File.WriteAllText(profile.ConfigFile, json);

        profile.SetConfigValue("Input", "use_unified_input_config", false);
        profile.UseStaticPatching();

        var config = JsonNode.Parse(File.ReadAllText(profile.ConfigFile))!.AsObject();
        Assert.False((bool)config[section]!["use_unified_input_config"]!);
        Assert.Single(config, s => s.Value is JsonObject o && o.ContainsKey("use_unified_input_config"));
        Assert.True((bool)config["General"]!["redzone_patches"]!);
    }

    [Fact]
    public void A_018_config_gets_static_patching_in_its_own_setting()
    {
        var profile = new EmulatorProfile(Path.Combine(_temp, "profile"));
        Directory.CreateDirectory(profile.UserDir);
        File.WriteAllText(profile.ConfigFile,
            """{ "WindowsGuestRedZoneProtection": { "windows_guest_red_zone_protection_mode": "Dynamic" } }""");

        profile.UseStaticPatching();

        var config = JsonNode.Parse(File.ReadAllText(profile.ConfigFile))!;
        Assert.Equal("StaticPatching", (string?)config["WindowsGuestRedZoneProtection"]!["windows_guest_red_zone_protection_mode"]);
        Assert.Null(config["General"]);
    }

    [Fact]
    public void Side_by_side_instances_always_get_a_window()
    {
        var profile = new EmulatorProfile(Path.Combine(_temp, "profile"));
        profile.ApplyDisplay(new DisplaySettings { ScreenMode = ScreenMode.Fullscreen }, windowed: true);

        var gpu = JsonNode.Parse(File.ReadAllText(profile.ConfigFile))!["GPU"]!;
        Assert.False((bool)gpu["full_screen"]!);
        Assert.Equal("Windowed", (string?)gpu["full_screen_mode"]);
    }

    [Fact]
    public void Shader_cache_is_measured_and_cleared()
    {
        var profile = new EmulatorProfile(Path.Combine(_temp, "profile"));
        var dir = Path.Combine(profile.UserDir, "cache", "CUSA03173");
        Directory.CreateDirectory(Path.Combine(dir, "sub"));
        File.WriteAllBytes(Path.Combine(dir, "sub", "a.spv"), new byte[100]);
        File.WriteAllBytes(Path.Combine(profile.UserDir, "cache", "CUSA03173.zip"), new byte[50]);
        File.WriteAllBytes(Path.Combine(profile.UserDir, "cache", "OTHER.zip"), new byte[7]);

        Assert.Equal(150, profile.ShaderCacheSize("CUSA03173"));
        profile.ClearShaderCache("CUSA03173");
        Assert.Equal(0, profile.ShaderCacheSize("CUSA03173"));
        Assert.True(File.Exists(Path.Combine(profile.UserDir, "cache", "OTHER.zip")));
    }

    [Fact]
    public void Shader_cache_is_rebuilt_when_shadps4_ignored_it_or_the_build_changed()
    {
        var profile = new EmulatorProfile(Path.Combine(_temp, "profile"));
        profile.EnsureCreated(seedFromUserDir: null);
        var exe = Path.Combine(_temp, "shadPS4.exe");
        File.WriteAllBytes(exe, new byte[10]);
        void MakeCache() => File.WriteAllBytes(Path.Combine(Directory.CreateDirectory(Path.Combine(profile.UserDir, "cache", "CUSA03173")).FullName, "a.spv"), new byte[100]);
        void Log(string line)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(profile.LogFile)!);
            File.WriteAllText(profile.LogFile, $"[Config] <Info> start\n{line}\n");
        }

        // First check: the cache is kept and the build noted.
        MakeCache();
        Log("[Render] <Info> (shadPS4:Main) vk_pipeline_serialization.cpp:371 WarmUp: Preloaded 434 pipelines");
        Assert.Equal(ShaderCacheReset.None, profile.KeepShaderCacheUsable("CUSA03173", exe));
        Assert.Equal(100, profile.ShaderCacheSize("CUSA03173"));

        // The last run ignored it (another GPU or driver).
        Log("[Render] <Warning> (shadPS4:Main) vk_pipeline_serialization.cpp:335 WarmUp: Pipeline cache isn't compatible with current system. Ignoring the cache");
        Assert.Equal(ShaderCacheReset.GpuOrDriverChanged, profile.KeepShaderCacheUsable("CUSA03173", exe));
        Assert.Equal(0, profile.ShaderCacheSize("CUSA03173"));

        // Another shadPS4 build.
        MakeCache();
        Log("WarmUp: Preloaded 434 pipelines");
        Assert.Equal(ShaderCacheReset.None, profile.KeepShaderCacheUsable("CUSA03173", exe));
        File.WriteAllBytes(exe, new byte[20]);
        Assert.Equal(ShaderCacheReset.EmulatorChanged, profile.KeepShaderCacheUsable("CUSA03173", exe));
        Assert.Equal(0, profile.ShaderCacheSize("CUSA03173"));
    }

    [Fact]
    public void Sharpness_percent_maps_to_rcas_attenuation()
    {
        Assert.Equal(88, new DisplaySettings().SharpnessPercent); // shadPS4's default 250
        Assert.Equal(2000, new DisplaySettings { SharpnessPercent = 0 }.RcasAttenuation);
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);
}
