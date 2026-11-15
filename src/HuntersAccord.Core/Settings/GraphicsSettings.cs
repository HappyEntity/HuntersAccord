namespace HuntersAccord.Core.Settings;

/// <summary>Picture and camera settings chosen in the game; they apply live to this player's own picture only. Percentages are of the area's own value (100 = as the game has it).</summary>
public sealed class GraphicsSettings
{
    /// <summary>The camera's field of view, percent of the game's (100..150).</summary>
    public int FieldOfView { get; set; } = 100;

    public int CameraDistance { get; set; } = 100;

    public int CameraHeight { get; set; } = 100;

    public int ShadowDistance { get; set; } = 100;

    public bool Ssao { get; set; } = true;

    /// <summary>How dark the ambient occlusion makes recesses, percent of the area's (0: none).</summary>
    public int AoStrength { get; set; } = 100;

    public bool AntiAliasing { get; set; } = true;

    /// <summary>The fog's opacity, percent of the area's (0: no fog).</summary>
    public int Fog { get; set; } = 100;

    public bool MotionBlur { get; set; } = true;

    public bool DepthOfField { get; set; } = true;

    public bool ChromaticAberration { get; set; } = true;

    /// <summary>The glare around bright lights, percent of the area's (0: none, which also skips its passes).</summary>
    public int Bloom { get; set; } = 100;

    /// <summary>Colour saturation, percent of the area's (0: black and white).</summary>
    public int Saturation { get; set; } = 100;

    public bool Vignette { get; set; } = true;

    public GraphicsSettings Clone() => (GraphicsSettings)MemberwiseClone();
}
