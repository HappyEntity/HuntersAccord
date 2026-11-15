namespace HuntersAccord.Core;

/// <summary>
/// Hunters' Accord's co-op compatibility number: two players' mods play together only when theirs are the same. It travels
/// wherever players meet — a summon sign, a host's placement, a session directory entry, the first message of the peer
/// handshake ("compat|…" before "activate|…") — and the game's title screen shows it as its "Server ver." (the Login
/// answer's ServerVersion, which the game only shows). A sign or placement of another number is not
/// offered to the game; two games of different numbers that meet anyway (a stale sign, a race, an older mod) refuse the
/// connection on both sides, each with a banner saying who must update.
/// </summary>
public static class CoopCompat
{
    /// <summary>
    /// This build's number. Bump it (by one) in a release whose mod cannot play with the previous release's: a peer
    /// message added, removed or changed in meaning or format (PeerLink's "…|…" messages, the game's P2P handled
    /// differently), the session directory's or Steam lobbies' data (SummonSign, HostPlacement, SessionEntry, lobby keys),
    /// anything both players' mods must agree on (world sync rules, flag lists both apply, the summon's steps). Not for a
    /// change only one player's game sees (the launcher, menus, texts, a fix inside one game). Numbers only grow: the
    /// higher one is the newer mod.
    /// </summary>
    public const int Number = 12;

    /// <summary>The number of a peer that told none: a mod from before the number existed (older than any).</summary>
    public const int Unknown = 0;

    /// <summary>This build's version (Directory.Build.props), as "0.2.0": told with the number, for the banners.</summary>
    public static string ModVersion { get; } = typeof(CoopCompat).Assembly.GetName().Version is { } v
        ? $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}"
        : "0";

    /// <summary>Whether a player of number <paramref name="theirs"/> plays with this one's <paramref name="mine"/>.</summary>
    public static bool Compatible(int mine, int theirs) => mine == theirs && theirs != Unknown;

    /// <summary>Which of two players is newer (numbers only grow; an unknown number is the older).</summary>
    public enum Side
    {
        Same,

        /// <summary>The other player's mod is newer: this player must update.</summary>
        TheyNewer,

        /// <summary>The other player's mod is older: the other player must update.</summary>
        TheyOlder,
    }

    public static Side Compare(int mine, int theirs) => theirs == mine ? Side.Same : theirs > mine ? Side.TheyNewer : Side.TheyOlder;

    /// <summary>
    /// A version as the banners show it: the version told ("?" when none: a mod from before the number), with the number
    /// after it when both versions read the same (developer builds between two releases).
    /// </summary>
    public static (string Mine, string Theirs) Labels(int mine, string myVersion, int theirs, string theirVersion)
    {
        var a = myVersion.Length > 0 ? myVersion : "?";
        var b = theirVersion.Length > 0 ? theirVersion : "?";
        return a == b ? ($"{a} #{mine}", $"{b} #{theirs}") : (a, b);
    }

    /// <summary>
    /// A version told by another player's mod, kept only when it reads as one ("0.2.0", "0.2.0-beta"): its text goes into
    /// a banner and a log line.
    /// </summary>
    public static string CleanVersion(string? version) =>
        version is { Length: > 0 and <= 24 } && version.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '+') ? version : "";

    /// <summary>
    /// Of what a search found: those of this number (<paramref name="mine"/>), and, when some were relevant (the password
    /// asked) but none of them is of this number, the newest other number among those with its version — what the player
    /// is told about ("a hunter with a newer mod was found").
    /// </summary>
    public static (T[] Taken, (int Number, string Version)? OnlyOther) Split<T>(IEnumerable<T> found, int mine,
        Func<T, int> number, Func<T, string> version, Func<T, bool> relevant) where T : class
    {
        var all = found as IReadOnlyCollection<T> ?? [.. found];
        T[] taken = [.. all.Where(f => Compatible(mine, number(f)))];
        if (taken.Any(relevant)) return (taken, null);
        var other = all.Where(f => relevant(f) && !Compatible(mine, number(f))).OrderByDescending(number).FirstOrDefault();
        return other is null ? (taken, null) : (taken, (number(other), CleanVersion(version(other))));
    }
}
