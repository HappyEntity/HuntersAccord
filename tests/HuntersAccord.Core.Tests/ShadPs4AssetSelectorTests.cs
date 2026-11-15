using HuntersAccord.Core.Emulator;

namespace HuntersAccord.Core.Tests;

public class ShadPs4AssetSelectorTests
{
    private static readonly List<GitHubAsset> Assets =
    [
        new() { Name = "shadps4-linux-sdl-0.18.0.zip" },
        new() { Name = "shadps4-macos-sdl-0.18.0.zip" },
        new() { Name = "shadps4-ubuntu64-0.18.0.zip" },
        new() { Name = "shadps4-win64-sdl-0.18.0.zip" },
    ];

    [Theory]
    [InlineData("win64", "shadps4-win64-sdl-0.18.0.zip")]
    [InlineData("linux", "shadps4-linux-sdl-0.18.0.zip")]
    [InlineData("macos", "shadps4-macos-sdl-0.18.0.zip")]
    public void Picks_sdl_build_for_os(string os, string expected) =>
        Assert.Equal(expected, ShadPs4AssetSelector.Select(Assets, os)?.Name);
}
