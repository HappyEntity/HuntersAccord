using HuntersAccord.Core.Emulator;

namespace HuntersAccord.Core.Tests;

public sealed class ShadPs4DownloaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ha-emulators-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Install(string tag, DateTime downloadedAt, bool marker = true)
    {
        var dir = Path.Combine(_root, tag);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, OperatingSystem.IsWindows() ? "shadPS4.exe" : "shadps4"), "");
        if (!marker) return;
        var markerFile = Path.Combine(dir, ".ha-release");
        File.WriteAllText(markerFile, tag);
        File.SetLastWriteTimeUtc(markerFile, downloadedAt);
    }

    [Fact]
    public void The_downloaded_build_is_found_without_the_network()
    {
        Install(ShadPs4Downloader.PinnedTag, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        Install("v.0.20.0", new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc));
        Install("v.0.22.0", new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc), marker: false); // an unfinished download

        Assert.Equal(ShadPs4Downloader.PinnedTag, ShadPs4Downloader.FindInstalled(EmulatorChannel.Pinned, _root)?.Tag);
        Assert.Equal("v.0.20.0", ShadPs4Downloader.FindInstalled(EmulatorChannel.Stable, _root)?.Tag);
        Assert.Null(ShadPs4Downloader.FindInstalled(EmulatorChannel.Pinned, Path.Combine(_root, "none")));
    }
}
