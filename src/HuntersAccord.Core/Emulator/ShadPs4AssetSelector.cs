using System.Runtime.InteropServices;

namespace HuntersAccord.Core.Emulator;

/// <summary>Picks the release asset matching the running OS. shadPS4 names them
/// <c>shadps4-{win64|linux|macos}-sdl-{version}.zip</c>.</summary>
internal static class ShadPs4AssetSelector
{
    public static string CurrentOsToken()
    {
        if (OperatingSystem.IsWindows()) return "win64";
        if (OperatingSystem.IsMacOS()) return "macos";
        if (OperatingSystem.IsLinux()) return "linux";
        throw new PlatformNotSupportedException($"shadPS4 has no build for {RuntimeInformation.OSDescription}");
    }

    public static GitHubAsset? Select(IEnumerable<GitHubAsset> assets, string osToken) =>
        assets.FirstOrDefault(a =>
            a.Name.StartsWith($"shadps4-{osToken}-sdl-", StringComparison.OrdinalIgnoreCase) &&
            a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
}
