using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;

namespace HuntersAccord.Core.Emulator;

/// <summary>
/// Downloads official shadPS4 builds from GitHub into <see cref="AppPaths.EmulatorsDir"/>. Hunters' Accord never redistributes the emulator;
/// this runs only after the player agreed, never at startup.
/// </summary>
public sealed class ShadPs4Downloader(HttpClient http)
{
    public const string Repository = "shadps4-emu/shadPS4";

    public const string RepositoryHost = "github.com/" + Repository;

    /// <summary>The build the guest-code hooks are verified against.</summary>
    public const string PinnedTag = "v.0.21.0";

    private const string MarkerFile = ".ha-release";

    private static string MarkerOf(string dir) => Path.Combine(dir, MarkerFile);

    public static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HuntersAccord-Launcher", "0.1"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public async Task<ShadPs4Release> ResolveAsync(EmulatorChannel channel, CancellationToken ct = default)
    {
        var os = ShadPs4AssetSelector.CurrentOsToken();

        if (channel == EmulatorChannel.Pinned)
        {
            var pinned = await GetAsync($"releases/tags/{PinnedTag}", GitHubJsonContext.Default.GitHubRelease, ct);
            return ToRelease(pinned, os);
        }

        var releases = await GetAsync("releases?per_page=20", GitHubJsonContext.Default.ListGitHubRelease, ct);
        var match = releases
            .Where(r => !r.Draft && (channel == EmulatorChannel.Nightly || !r.PreRelease))
            .OrderByDescending(r => r.PublishedAt)
            .FirstOrDefault(r => ShadPs4AssetSelector.Select(r.Assets, os) is not null)
            ?? throw new InvalidOperationException($"No shadPS4 {channel} release has a '{os}' build.");
        return ToRelease(match, os);
    }

    /// <summary>Downloads and unpacks the release; returns the path of the emulator executable.</summary>
    public async Task<string> InstallAsync(ShadPs4Release release, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var target = GetInstallDir(release.Tag);
        if (TryGetExecutable(target) is { } existing && File.Exists(MarkerOf(target)))
            return existing;

        Directory.CreateDirectory(AppPaths.EmulatorsDir);
        var zipPath = Path.Combine(AppPaths.EmulatorsDir, release.AssetName + ".part");

        using (var response = await http.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? release.Size;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var file = File.Create(zipPath);
            var buffer = new byte[1 << 16];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                received += read;
                if (total > 0) progress?.Report((double)received / total);
            }
        }

        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        await ZipFile.ExtractToDirectoryAsync(zipPath, target, overwriteFiles: true, ct);
        File.Delete(zipPath);
        await File.WriteAllTextAsync(Path.Combine(target, MarkerFile), release.Tag, ct);

        return TryGetExecutable(target)
               ?? throw new FileNotFoundException($"shadPS4 executable not found in {release.AssetName}.");
    }

    public sealed record InstalledBuild(string Tag, string Executable);

    /// <summary>The build already on this PC for a channel, found without the network: the pinned tag for <see cref="EmulatorChannel.Pinned"/>, else the latest download.</summary>
    public static InstalledBuild? FindInstalled(EmulatorChannel channel, string? emulatorsDir = null)
    {
        var root = emulatorsDir ?? AppPaths.EmulatorsDir;
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateDirectories(root)
            .Select(dir => (Dir: dir, Marker: MarkerOf(dir)))
            .Where(b => File.Exists(b.Marker))
            .Select(b => (Tag: File.ReadAllText(b.Marker).Trim(), Executable: TryGetExecutable(b.Dir), At: File.GetLastWriteTimeUtc(b.Marker)))
            .Where(b => b.Executable is not null && (channel != EmulatorChannel.Pinned || b.Tag == PinnedTag))
            .OrderByDescending(b => b.At)
            .Select(b => new InstalledBuild(b.Tag, b.Executable!))
            .FirstOrDefault();
    }

    public static string GetInstallDir(string tag) =>
        Path.Combine(AppPaths.EmulatorsDir, string.Concat(tag.Split(Path.GetInvalidFileNameChars())));

    /// <summary>Finds the emulator binary inside an unpacked build (layout differs per OS).</summary>
    public static string? TryGetExecutable(string dir)
    {
        if (!Directory.Exists(dir)) return null;
        string[] names = OperatingSystem.IsWindows() ? ["shadPS4.exe"]
            : OperatingSystem.IsMacOS() ? ["shadps4", "shadPS4"]
            : ["Shadps4-sdl.AppImage", "shadps4", "shadPS4"];

        foreach (var name in names)
        {
            var hit = Directory.EnumerateFiles(dir, name, new EnumerationOptions
            {
                RecurseSubdirectories = true,
                MatchCasing = MatchCasing.CaseInsensitive,
            }).FirstOrDefault();
            if (hit is not null) return hit;
        }
        return null;
    }

    private async Task<T> GetAsync<T>(string relative, JsonTypeInfo<T> typeInfo, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{Repository}/{relative}";
        return await http.GetFromJsonAsync(url, typeInfo, ct)
               ?? throw new InvalidOperationException($"Empty GitHub response for {url}");
    }

    private static ShadPs4Release ToRelease(GitHubRelease r, string os)
    {
        var asset = ShadPs4AssetSelector.Select(r.Assets, os)
                    ?? throw new InvalidOperationException($"shadPS4 {r.TagName} has no '{os}' build.");
        return new ShadPs4Release(r.TagName, r.Name ?? r.TagName, r.PreRelease, r.PublishedAt,
            asset.Name, new Uri(asset.DownloadUrl), asset.Size);
    }
}
