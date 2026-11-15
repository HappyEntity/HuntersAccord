using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HuntersAccord.Core.Updates;

/// <summary>A newer Hunters' Accord release: its version and its page (for the player to open; nothing is downloaded).</summary>
public sealed record NewVersion(string Version, string PageUrl);

/// <summary>At most once a day, reads the latest release tag of the public repository and compares it with the running version. It never downloads anything; when off, or on any failure, nothing happens.</summary>
public static class UpdateCheck
{
    public const string Repository = "HappyEntity/HuntersAccord";

    public const string LatestReleaseApi = "https://api.github.com/repos/" + Repository + "/releases/latest";

    public const string ReleasesPage = "https://github.com/" + Repository + "/releases";

    /// <summary>At most one request this often (an answer is remembered until then).</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    private static string StatePath => Path.Combine(AppPaths.DataRoot, "update-check.json");

    /// <summary>The newer release, or null (none, not checked, unreachable). Asks GitHub at most once a day; otherwise answers from the state at <paramref name="statePath"/>.</summary>
    public static async Task<NewVersion?> CheckAsync(HttpClient http, string currentVersion, string? statePath = null,
        DateTimeOffset? now = null, CancellationToken cancel = default)
    {
        statePath ??= StatePath;
        var at = now ?? DateTimeOffset.UtcNow;
        var state = LoadState(statePath);
        if (state is null || at - state.CheckedAt >= Interval || at < state.CheckedAt)
        {
            var latest = await LatestAsync(http, cancel);
            state = new CheckState { CheckedAt = at, Tag = latest?.Tag, PageUrl = latest?.PageUrl };
            SaveState(statePath, state);
        }
        return state.Tag is { } tag && IsNewer(tag, currentVersion)
            ? new NewVersion(Normalize(tag), string.IsNullOrEmpty(state.PageUrl) ? ReleasesPage : state.PageUrl)
            : null;
    }

    /// <summary>An HttpClient for the check: short timeout, the User-Agent GitHub's API wants.</summary>
    public static HttpClient CreateHttpClient(string currentVersion)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HuntersAccord-Launcher",
            string.IsNullOrEmpty(currentVersion) ? "0" : currentVersion));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static bool IsNewer(string tag, string current) =>
        Parse(tag) is { } latest && Parse(current) is { } running && latest > running;

    private static string Normalize(string tag) => tag.Trim().TrimStart('v', 'V');

    private static Version? Parse(string text)
    {
        var core = Normalize(text).Split('+', '-')[0];
        if (!Version.TryParse(core.Contains('.') ? core : core + ".0", out var v)) return null;
        // 0.1 and 0.1.0 are the same version.
        return new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
    }

    private static async Task<(string Tag, string? PageUrl)?> LatestAsync(HttpClient http, CancellationToken cancel)
    {
        try
        {
            using var response = await http.GetAsync(LatestReleaseApi, cancel);
            if (!response.IsSuccessStatusCode) return null; // 404: no public release (yet), rate limits...
            var release = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(cancel),
                UpdateJsonContext.Default.LatestRelease);
            return release is { TagName.Length: > 0, Draft: false, PreRelease: false } ? (release.TagName, release.HtmlUrl) : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or IOException
                                      or InvalidOperationException)
        {
            return null;
        }
    }

    private static CheckState? LoadState(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize(File.ReadAllText(path), UpdateJsonContext.Default.CheckState) : null;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void SaveState(string path, CheckState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(state, UpdateJsonContext.Default.CheckState));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Not remembered: asked again at the next start, no harm.
        }
    }

    internal sealed class CheckState
    {
        public DateTimeOffset CheckedAt { get; set; }
        public string? Tag { get; set; }
        public string? PageUrl { get; set; }
    }

    internal sealed class LatestRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool PreRelease { get; set; }
    }
}

[JsonSerializable(typeof(UpdateCheck.CheckState))]
[JsonSerializable(typeof(UpdateCheck.LatestRelease))]
internal sealed partial class UpdateJsonContext : JsonSerializerContext;
