using System.Globalization;

namespace HuntersAccord.Core.Game;

/// <summary>The shape of a resolution, as the launcher groups them.</summary>
public enum AspectKind
{
    Standard, // 16:9
    Tall,     // 16:10
    Wide,     // 21:9
    SuperWide, // 32:9
    Other,
}

/// <summary>
/// The size the game renders at and what follows from it: the 3D view's aspect, where the 16:9 menus and HUD sit in the frame, and the extra graphics memory a larger frame needs.
/// <para>
/// The game's render passes and display buffers read their size once at start, so it is chosen before the game starts (passed in <see cref="EnvironmentVariable"/>).
/// The menus' 1920x1080 stage stays whole and centred in any frame; the 3D view keeps the game's vertical field of view and sees more across on a wider frame (Hor+).
/// </para>
/// </summary>
public readonly record struct ScreenResolution(int Width, int Height)
{
    /// <summary>The mod reads the chosen size from this variable ("2560x1080"); unset or invalid: <see cref="Stock"/>.</summary>
    public const string EnvironmentVariable = "HA_RESOLUTION";

    public const int StageWidth = 1920, StageHeight = 1080;

    public const double StageAspect = (double)StageWidth / StageHeight;

    public const int MinWidth = 1280, MinHeight = 720, MaxWidth = 7680, MaxHeight = 4320;

    public static ScreenResolution Stock => new(StageWidth, StageHeight);

    public static IReadOnlyList<ScreenResolution> Presets { get; } =
    [
        new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3840, 2160),
        new(1920, 1200), new(2560, 1600),
        new(2560, 1080), new(3440, 1440),
        new(3840, 1080), new(5120, 1440),
    ];

    /// <summary>The most pixels the game is started with: 4K's. A larger frame drew the world broken under shadPS4 0.20.</summary>
    public const long MaxPixels = 3840L * 2160;

    public bool IsStock => Width == StageWidth && Height == StageHeight;

    public double Aspect => (double)Width / Height;

    public long Pixels => (long)Width * Height;

    /// <summary>16:9 (within 1 %: 1366x768 counts), 16:10, 21:9 (64:27 to 43:18), 32:9, or another shape.</summary>
    public AspectKind Kind => Aspect switch
    {
        var a when Near(a, 16.0 / 9) => AspectKind.Standard,
        var a when Near(a, 16.0 / 10) => AspectKind.Tall,
        var a when a is >= 2.3 and <= 2.45 => AspectKind.Wide,
        var a when Near(a, 32.0 / 9) => AspectKind.SuperWide,
        _ => AspectKind.Other,
    };

    private static bool Near(double a, double b) => Math.Abs(a / b - 1) < 0.01;

    /// <summary>A size the game is started at: even sides within the bounds, no more pixels than 4K, from 4:3 to 32:9.</summary>
    public bool IsSupported => Width is >= MinWidth and <= MaxWidth && Height is >= MinHeight and <= MaxHeight && Pixels <= MaxPixels &&
                               Width % 2 == 0 && Height % 2 == 0 && Aspect is >= 4.0 / 3 - 0.001 and <= 32.0 / 9 + 0.001;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Width}x{Height}");

    /// <summary>"2560x1080" (also "2560×1080", spaces allowed); false for anything else.</summary>
    public static bool TryParse(string? text, out ScreenResolution resolution)
    {
        resolution = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Replace('×', 'x').Replace('X', 'x').Split('x', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var w) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var h))
            return false;
        resolution = new ScreenResolution(w, h);
        return true;
    }

    public static ScreenResolution FromEnvironment(string? value) =>
        TryParse(value, out var r) && r.IsSupported ? r : Stock;


    /// <summary>What the render-target heap gets on top of its stock 2287 MiB, in MiB: about 270 MiB per million pixels past 1080p plus 256 MiB to spare, in 64 MiB steps; none at 1080p or less.</summary>
    public int ExtraGraphicsMemoryMiB
    {
        get
        {
            var past = Pixels - (long)StageWidth * StageHeight;
            if (past <= 0) return 0;
            var mib = 270.0 * past / 1e6 + 256;
            return (int)Math.Ceiling(mib / 64) * 64;
        }
    }

    /// <summary>shadPS4's extra direct memory (<c>General.extra_dmem_in_mbytes</c>) for this size: the heap grows inside the game's direct memory, which the stock arenas already take nearly whole.</summary>
    public int ExtraDirectMemoryMiB => ExtraGraphicsMemoryMiB;


    public double WideFactor => Math.Max(1, Aspect / StageAspect);

    public double TallFactor => Math.Max(1, StageAspect / Aspect);

    /// <summary>The frame's edges in stage coordinates (the stage is the frame's centre), in whole stage pixels, rounded out.</summary>
    public (int Left, int Top, int Right, int Bottom) StageExtent
    {
        get
        {
            var mx = (int)Math.Ceiling(StageWidth / 2.0 * (WideFactor - 1) - 1e-9);
            var my = (int)Math.Ceiling(StageHeight / 2.0 * (TallFactor - 1) - 1e-9);
            return (-mx, -my, StageWidth + mx, StageHeight + my);
        }
    }

    /// <summary>The game's projection of a point onto the stage for floating plates: x = (ndc.x · X + 0.5) · 1920, y = (ndc.y · Y + 0.5) · 1080. A frame wider than the stage reaches past it, so the scale grows by as much.</summary>
    public (float X, float Y) PlateScale => ((float)(0.5 * WideFactor), (float)(-0.5 * TallFactor));

    /// <summary>The 3D view's aspect (the cameras' width / height): the frame's own (Hor+ at the game's vertical FOV).</summary>
    public float CameraAspect => (float)Aspect;

    /// <summary>The game's cameras' aspect: 16:9, held in each camera beside its vertical field of view.</summary>
    public const float GameCameraAspect = 16f / 9f;

    /// <summary>Where shadPS4 shows the frame in a window (its FitImage: whole and centred, kept in shape).</summary>
    public (double X, double Y, double Width, double Height) FrameIn(double windowWidth, double windowHeight)
    {
        if (windowWidth <= 0 || windowHeight <= 0) return (0, 0, 0, 0);
        var frame = Aspect;
        double w = windowWidth, h = windowHeight;
        if (frame > windowWidth / windowHeight) h = Math.Floor(windowWidth / frame);
        else w = Math.Floor(windowHeight * frame);
        return (Math.Floor((windowWidth - w) / 2), Math.Floor((windowHeight - h) / 2), w, h);
    }

    /// <summary>Where the menus' 1920x1080 stage is in a window: whole and centred in the frame placed as <see cref="FrameIn"/>. A stage point p is at <c>offset + p · scale</c>.</summary>
    public (double Scale, double OffsetX, double OffsetY) StageIn(double windowWidth, double windowHeight)
    {
        var (fx, fy, fw, fh) = FrameIn(windowWidth, windowHeight);
        if (fw <= 0 || fh <= 0) return (0, 0, 0);
        var scale = Math.Min(fw / StageWidth, fh / StageHeight);
        return (scale, fx + (fw - StageWidth * scale) / 2, fy + (fh - StageHeight * scale) / 2);
    }


    /// <summary>Window sizes of this resolution's shape (heights 720 … 2160) up to the given maximum, smallest first; at least the smallest one.</summary>
    public IReadOnlyList<(int Width, int Height)> WindowSizes(int maxWidth, int maxHeight)
    {
        int[] heights = [720, 900, 1080, 1200, 1440, 1600, 2160];
        var sizes = new SortedSet<(int, int)>(Comparer<(int W, int H)>.Create((a, b) => (a.W * (long)a.H).CompareTo(b.W * (long)b.H) is var c && c != 0 ? c : a.W.CompareTo(b.W)));
        foreach (var h in heights)
        {
            var w = (int)Math.Round(h * Aspect / 2) * 2;
            if (w <= maxWidth && h <= maxHeight) sizes.Add((w, h));
        }
        if (Width <= maxWidth && Height <= maxHeight) sizes.Add((Width, Height));
        if (sizes.Count == 0) sizes.Add(((int)Math.Round(heights[0] * Aspect / 2) * 2, heights[0]));
        return [.. sizes];
    }

    /// <summary>The window size of this shape nearest to <paramref name="height"/> among <see cref="WindowSizes"/>.</summary>
    public (int Width, int Height) WindowNear(int height, int maxWidth, int maxHeight) =>
        WindowSizes(maxWidth, maxHeight).MinBy(s => Math.Abs(s.Height - height));

    /// <summary>Whether a window of this size shows the frame without bars (the same shape, within a pixel).</summary>
    public bool Fills(int windowWidth, int windowHeight) =>
        windowHeight > 0 && Math.Abs(windowWidth - windowHeight * Aspect) <= 1.0 + 1e-9;
}
