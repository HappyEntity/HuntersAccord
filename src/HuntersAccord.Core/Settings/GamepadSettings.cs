namespace HuntersAccord.Core.Settings;

/// <summary>The gamepad as set up in the launcher, written to shadPS4's input config at every start.</summary>
public sealed class GamepadSettings
{
    /// <summary>PS4 buttons the player moved: output ("r1") to the gamepad button that gives it ("cross"), or <see cref="Unassigned"/>.</summary>
    public Dictionary<string, string> Buttons { get; set; } = [];

    public const string Unassigned = "";

    public bool SwapSticks { get; set; }

    public int LeftDeadZonePercent { get; set; } = DefaultDeadZonePercent;

    public int RightDeadZonePercent { get; set; } = DefaultDeadZonePercent;

    /// <summary>4 % ≈ shadPS4's own 5 of 127.</summary>
    public const int DefaultDeadZonePercent = 4, MaxDeadZonePercent = 40;

    public bool OwnLightColor { get; set; }

    /// <summary>"#RRGGBB"; black turns the light bar off.</summary>
    public string LightColor { get; set; } = DefaultLightColor;

    public const string DefaultLightColor = "#B3141E";
}
