namespace HuntersAccord.Core.Emulator;

public enum EmulatorChannel
{
    /// <summary>The release Hunters' Accord was tested against. Default.</summary>
    Pinned,
    Stable,
    /// <summary>Latest nightly pre-release. May break guest hooks.</summary>
    Nightly,
}

public sealed record ShadPs4Release(
    string Tag,
    string Name,
    bool IsPreRelease,
    DateTimeOffset PublishedAt,
    string AssetName,
    Uri DownloadUrl,
    long Size)
{
    /// <summary>Short name for the launcher: "shadPS4 v0.21.0" (the tag without the dot after "v"), not the release title.</summary>
    public string DisplayName => DisplayNameOf(Tag);

    public string ReleasePage => $"https://{ShadPs4Downloader.RepositoryHost}/releases/tag/{Uri.EscapeDataString(Tag)}";

    public long SizeMb => (Size + 524288) / 1048576;

    public static string DisplayNameOf(string tag) => "shadPS4 " + (tag.StartsWith("v.", StringComparison.Ordinal) ? "v" + tag[2..] : tag);
}
