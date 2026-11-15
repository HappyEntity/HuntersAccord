using System.Text.Json.Nodes;
using HuntersAccord.Core.Emulator;
using HuntersAccord.Core.Game;
using HuntersAccord.Core.Settings;

namespace HuntersAccord.Core.Tests;

public sealed class ScreenResolutionTests
{
    [Theory]
    [InlineData("2560x1080", 2560, 1080)]
    [InlineData(" 3440 × 1440 ", 3440, 1440)]
    [InlineData("1920X1200", 1920, 1200)]
    public void Sizes_are_read_as_width_x_height(string text, int w, int h)
    {
        Assert.True(ScreenResolution.TryParse(text, out var r));
        Assert.Equal(new ScreenResolution(w, h), r);
        Assert.Equal($"{w}x{h}", r.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2560")]
    [InlineData("2560x")]
    [InlineData("-2560x1080")]
    [InlineData("640x480")]     // smaller than the game is started at
    [InlineData("4000x1000")]   // wider than 32:9
    [InlineData("2561x1080")]   // odd
    [InlineData("5120x2160")]   // more pixels than 4K: the world drew broken under shadPS4 0.20
    public void Anything_else_is_the_games_own_size(string? text) =>
        Assert.Equal(ScreenResolution.Stock, ScreenResolution.FromEnvironment(text));

    [Fact]
    public void Every_preset_is_supported_and_has_its_shape()
    {
        Assert.All(ScreenResolution.Presets, p => Assert.True(p.IsSupported, p.ToString()));
        Assert.Contains(ScreenResolution.Stock, ScreenResolution.Presets);
        Assert.Equal(AspectKind.Standard, new ScreenResolution(2560, 1440).Kind);
        Assert.Equal(AspectKind.Standard, new ScreenResolution(1366, 768).Kind);
        Assert.Equal(AspectKind.Tall, new ScreenResolution(2560, 1600).Kind);
        Assert.Equal(AspectKind.Wide, new ScreenResolution(2560, 1080).Kind);
        Assert.Equal(AspectKind.Wide, new ScreenResolution(3440, 1440).Kind);
        Assert.Equal(AspectKind.SuperWide, new ScreenResolution(5120, 1440).Kind);
        Assert.Equal(AspectKind.Other, new ScreenResolution(1440, 1080).Kind);
        foreach (var kind in new[] { AspectKind.Standard, AspectKind.Tall, AspectKind.Wide, AspectKind.SuperWide })
            Assert.Contains(ScreenResolution.Presets, p => p.Kind == kind);
    }

    [Fact]
    public void Graphics_memory_grows_only_past_1080p_in_64_MiB_steps()
    {
        Assert.Equal(0, ScreenResolution.Stock.ExtraGraphicsMemoryMiB);
        Assert.Equal(0, new ScreenResolution(1280, 720).ExtraGraphicsMemoryMiB);
        var sizes = new[] { new ScreenResolution(2560, 1080), new(2560, 1440), new(3440, 1440), new(5120, 1440), new(3840, 2160) };
        var mib = sizes.Select(s => s.ExtraGraphicsMemoryMiB).ToArray();
        Assert.All(mib, m => Assert.Equal(0, m % 64));
        Assert.Equal(mib.Order(), mib); // more pixels, more memory
        Assert.InRange(new ScreenResolution(3840, 2160).ExtraGraphicsMemoryMiB, 1900, 2000); // 6.2 MP past 1080p × 270 + 256
        Assert.Equal(new ScreenResolution(2560, 1440).ExtraGraphicsMemoryMiB, new ScreenResolution(2560, 1440).ExtraDirectMemoryMiB);
    }

    [Fact]
    public void At_16_9_the_stage_is_the_frame_and_the_plates_keep_the_games_scale()
    {
        foreach (var r in new[] { ScreenResolution.Stock, new ScreenResolution(2560, 1440), new ScreenResolution(1280, 720) })
        {
            Assert.Equal((0, 0, 1920, 1080), r.StageExtent);
            Assert.Equal((0.5f, -0.5f), r.PlateScale);
        }
    }

    [Fact]
    public void A_wider_frame_reaches_past_the_stage_at_the_sides()
    {
        var wide = new ScreenResolution(2560, 1080); // 64:27, a third wider than 16:9
        Assert.Equal((-320, 0, 2240, 1080), wide.StageExtent);
        Assert.Equal(2f / 3, wide.PlateScale.X, 4);
        Assert.Equal(-0.5f, wide.PlateScale.Y);
        Assert.Equal((-960, 0, 2880, 1080), new ScreenResolution(3840, 1080).StageExtent); // 32:9: twice as wide
        // The plates' projection lands the frame's edges on the extent: x = (ndc · X + 0.5) · 1920.
        Assert.Equal(-320, (-1 * wide.PlateScale.X + 0.5) * 1920, 1);
        Assert.Equal(2240, (1 * wide.PlateScale.X + 0.5) * 1920, 1);
    }

    [Fact]
    public void A_taller_frame_reaches_past_it_above_and_below()
    {
        var tall = new ScreenResolution(1920, 1200);
        Assert.Equal((0, -60, 1920, 1140), tall.StageExtent);
        Assert.Equal(0.5f, tall.PlateScale.X);
        Assert.Equal(-0.5f * 1200 / 1080, tall.PlateScale.Y, 4);
    }

    [Fact]
    public void The_cameras_take_the_frames_aspect()
    {
        Assert.Equal(ScreenResolution.GameCameraAspect, ScreenResolution.Stock.CameraAspect, 5);
        Assert.Equal(2560f / 1080, new ScreenResolution(2560, 1080).CameraAspect, 5);
        Assert.Equal(1.6f, new ScreenResolution(2560, 1600).CameraAspect, 5);
    }

    [Fact]
    public void The_frame_is_fitted_in_the_window_as_shadps4_does()
    {
        var wide = new ScreenResolution(2560, 1080);
        Assert.Equal((0, 0, 2560, 1080), wide.FrameIn(2560, 1080));
        // A 16:9 window: the wide frame across, bars above and below.
        Assert.Equal((0, 135, 1920, 810), wide.FrameIn(1920, 1080));
        // A 16:9 frame in a 21:9 window: bars at the sides.
        Assert.Equal((320, 0, 1920, 1080), ScreenResolution.Stock.FrameIn(2560, 1080));
    }

    [Fact]
    public void The_stage_is_centred_in_the_frame_in_the_window()
    {
        // 21:9 frame filling its window: the stage is the middle 16:9.
        var (scale, ox, oy) = new ScreenResolution(2560, 1080).StageIn(2560, 1080);
        Assert.Equal(1, scale, 6);
        Assert.Equal(320, ox, 6);
        Assert.Equal(0, oy, 6);
        // Half-size window of the same shape: everything halves.
        (scale, ox, oy) = new ScreenResolution(2560, 1080).StageIn(1280, 540);
        Assert.Equal(0.5, scale, 6);
        Assert.Equal(160, ox, 6);
        // A 21:9 frame in a 16:9 window: letterboxed first, then the stage pillarboxed inside it.
        (scale, ox, oy) = new ScreenResolution(2560, 1080).StageIn(1920, 1080);
        Assert.Equal(0.75, scale, 6);
        Assert.Equal(240, ox, 6);
        Assert.Equal(135, oy, 6);
        // The game's own size: as before (the stage is the frame, fitted in the window).
        (scale, ox, oy) = ScreenResolution.Stock.StageIn(1280, 720);
        Assert.Equal((2.0 / 3, 0.0, 0.0), (Math.Round(scale, 6), ox, oy), new RoundedComparer());
        // A stage point maps back to itself through the window.
        var r = new ScreenResolution(3440, 1440);
        var (s, x0, y0) = r.StageIn(1720, 720);
        var windowX = x0 + 960 * s;
        Assert.Equal(860, windowX, 6); // the stage's centre is the window's
        Assert.Equal(360, y0 + 540 * s, 6);
    }

    private sealed class RoundedComparer : IEqualityComparer<(double, double, double)>
    {
        public bool Equals((double, double, double) a, (double, double, double) b) =>
            Math.Abs(a.Item1 - b.Item1) < 1e-6 && Math.Abs(a.Item2 - b.Item2) < 1e-6 && Math.Abs(a.Item3 - b.Item3) < 1e-6;

        public int GetHashCode((double, double, double) v) => 0;
    }

    [Fact]
    public void Window_sizes_have_the_resolutions_shape_and_fit_the_monitor()
    {
        var wide = new ScreenResolution(3440, 1440);
        var sizes = wide.WindowSizes(3440, 1440);
        Assert.All(sizes, s => Assert.True(wide.Fills(s.Width, s.Height) || Math.Abs(s.Width - s.Height * wide.Aspect) <= 2, s.ToString()));
        Assert.All(sizes, s => Assert.True(s.Width <= 3440 && s.Height <= 1440));
        Assert.Contains((3440, 1440), sizes);
        Assert.Contains((1720, 720), sizes);
        Assert.Equal(sizes.OrderBy(s => s.Width * (long)s.Height), sizes);
        // A 1080p monitor: no window taller than it.
        Assert.DoesNotContain(ScreenResolution.Stock.WindowSizes(1920, 1080), s => s.Height > 1080);
        Assert.Contains((1280, 720), ScreenResolution.Stock.WindowSizes(1920, 1080));
        // Nothing fits: the smallest of the shape still.
        Assert.Single(new ScreenResolution(5120, 1440).WindowSizes(800, 600));
    }

    [Fact]
    public void A_window_of_another_shape_gives_way_to_the_nearest_of_this_one()
    {
        var wide = new ScreenResolution(2560, 1080);
        Assert.False(wide.Fills(1280, 720));
        Assert.Equal(720, wide.WindowNear(720, 2560, 1440).Height);
        Assert.True(wide.Fills(wide.WindowNear(1000, 2560, 1440).Width, wide.WindowNear(1000, 2560, 1440).Height) ||
                    Math.Abs(wide.WindowNear(1000, 2560, 1440).Width - wide.WindowNear(1000, 2560, 1440).Height * wide.Aspect) <= 2);
    }

    [Fact]
    public void Display_settings_keep_the_resolution_and_give_shadps4_the_memory_it_needs()
    {
        var temp = Path.Combine(Path.GetTempPath(), "ha-res-" + Guid.NewGuid().ToString("N"));
        try
        {
            var profile = new EmulatorProfile(Path.Combine(temp, "profile"));
            Directory.CreateDirectory(profile.UserDir);
            File.WriteAllText(profile.ConfigFile, """{ "General": { "extra_dmem_in_mbytes": 4000 } }""");
            var display = new DisplaySettings { Resolution = new ScreenResolution(3440, 1440) };
            profile.ApplyDisplay(display);
            var config = JsonNode.Parse(File.ReadAllText(profile.ConfigFile))!;
            Assert.Equal(new ScreenResolution(3440, 1440).ExtraDirectMemoryMiB, (int)config["General"]!["extra_dmem_in_mbytes"]!);

            profile.ApplyDisplay(new DisplaySettings());
            config = JsonNode.Parse(File.ReadAllText(profile.ConfigFile))!;
            Assert.Equal(0, (int)config["General"]!["extra_dmem_in_mbytes"]!); // the game's size: none (a seeded 4000 undone)
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void A_stored_size_the_game_does_not_start_at_counts_as_its_own()
    {
        Assert.Equal(ScreenResolution.Stock, new DisplaySettings { ResolutionWidth = 123, ResolutionHeight = 45 }.Resolution);
        Assert.Equal(ScreenResolution.Stock, new DisplaySettings().Resolution);
        Assert.Equal(new ScreenResolution(2560, 1080), new DisplaySettings { ResolutionWidth = 2560, ResolutionHeight = 1080 }.Resolution);
    }

    [Fact]
    public void The_launcher_passes_a_size_other_than_the_games_own_to_the_mod()
    {
        Assert.Equal("HA_RESOLUTION", ScreenResolution.EnvironmentVariable);
        Assert.Equal(new ScreenResolution(2560, 1080), ScreenResolution.FromEnvironment(new ScreenResolution(2560, 1080).ToString()));
    }
}
