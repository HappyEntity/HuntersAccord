using System.Text.Json.Serialization;

namespace HuntersAccord.Core.Settings;

public enum ScreenMode
{
    Windowed,
    Borderless,
    Fullscreen,
}

/// <summary>shadPS4's Vulkan present mode: Mailbox and FIFO wait for the display, Off does not (may tear).</summary>
public enum VSyncMode
{
    Mailbox,
    Fifo,
    Off,
}

/// <summary>shadPS4's picture settings, written to the profile's config.json at every start.</summary>
public sealed class DisplaySettings
{
    public ScreenMode ScreenMode { get; set; } = ScreenMode.Windowed;

    /// <summary>The game's render size, set at the game's start; 1920x1080 is the game's own.</summary>
    public int ResolutionWidth { get; set; } = Game.ScreenResolution.StageWidth;

    public int ResolutionHeight { get; set; } = Game.ScreenResolution.StageHeight;

    /// <summary>The render size; a stored size the game does not start at counts as 1920x1080.</summary>
    [JsonIgnore]
    public Game.ScreenResolution Resolution
    {
        get => new Game.ScreenResolution(ResolutionWidth, ResolutionHeight) is { IsSupported: true } r ? r : Game.ScreenResolution.Stock;
        set => (ResolutionWidth, ResolutionHeight) = (value.Width, value.Height);
    }

    /// <summary>Window size in windowed mode; fullscreen takes the monitor's resolution.</summary>
    public int WindowWidth { get; set; } = 1280;

    public int WindowHeight { get; set; } = 720;

    public VSyncMode VSync { get; set; } = VSyncMode.Mailbox;

    public bool Fsr { get; set; }

    /// <summary>FSR sharpening (RCAS) attenuation as shadPS4 keeps it: 0 sharpest .. 2000 softest (default 250).</summary>
    public int RcasAttenuation { get; set; } = DefaultRcasAttenuation;

    public const int DefaultRcasAttenuation = 250, MaxRcasAttenuation = 2000;

    [JsonIgnore]
    public int SharpnessPercent
    {
        get => (int)Math.Round(100 - RcasAttenuation * 100.0 / MaxRcasAttenuation);
        set => RcasAttenuation = (int)Math.Round((100 - Math.Clamp(value, 0, 100)) * MaxRcasAttenuation / 100.0);
    }

    public bool ShowFps { get; set; }

    public bool ShaderCache { get; set; } = true;

    /// <summary>shadPS4 hotkeys the player turned off (config names, e.g. "hotkey_pause"): written with no key.</summary>
    public List<string> HotkeysOff { get; set; } = [];
}
