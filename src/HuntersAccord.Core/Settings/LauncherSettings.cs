using System.Text.Json;
using System.Text.Json.Serialization;
using HuntersAccord.Core.Emulator;

namespace HuntersAccord.Core.Settings;

public sealed class LauncherSettings
{
    public string Language { get; set; } = "en";

    public EmulatorChannel EmulatorChannel { get; set; } = EmulatorChannel.Pinned;

    /// <summary>AppID of the FromSoftware game the Steam session runs under; 0 = the default (ELDEN RING).</summary>
    public uint SteamAppId { get; set; }

    /// <summary>The steam_api64.dll the player chose; null = the newest fit one among the installed Steam games.</summary>
    public string? SteamApiPath { get; set; }

    /// <summary>How co-op players meet (developer mode can test on this PC alone; otherwise always Steam).</summary>
    public CoopNetwork CoopNetwork { get; set; } = CoopNetwork.Steam;

    public string? GameSourceDir { get; set; }

    /// <summary>PS4 system language for the game; null = follow the launcher language.</summary>
    public int? GameLanguage { get; set; }

    /// <summary>Where PKGs are unpacked to; null = next to the PKG files.</summary>
    public string? InstallDir { get; set; }

    public string? GamePath { get; set; }

    public bool UiSounds { get; set; } = true;

    /// <summary>Once a day, reads the public repository's latest release tag to announce a newer version. Off: no request at all.</summary>
    public bool CheckForUpdates { get; set; } = true;

    public bool HideEmulatorConsole { get; set; } = true;

    public DisplaySettings Display { get; set; } = new();

    public GamepadSettings Gamepad { get; set; } = new();
}

public enum CoopNetwork
{
    Steam,

    ThisPc,
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(LauncherSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;

public static class SettingsStore
{
    public static LauncherSettings Load(string? path = null)
    {
        path ??= AppPaths.SettingsFile;
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize(File.ReadAllText(path), SettingsJsonContext.Default.LauncherSettings)
                       ?? new LauncherSettings();
        }
        catch (JsonException)
        {
            // Corrupted settings fall back to defaults instead of blocking the launcher.
        }
        return new LauncherSettings();
    }

    public static void Save(LauncherSettings settings, string? path = null)
    {
        path ??= AppPaths.SettingsFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.LauncherSettings));
    }
}
