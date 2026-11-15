namespace HuntersAccord.Core.Steam;

/// <summary>
/// A FromSoftware title on Steam. Hunters' Accord runs its Steam session under the AppID of a FromSoftware
/// game the player owns, as a tribute to the studio.
/// </summary>
public sealed record FromSoftwareGame(uint AppId, string Name, int Priority);

public static class FromSoftwareGames
{
    /// <summary>
    /// Known titles. Lower <see cref="FromSoftwareGame.Priority"/> is preferred when several are owned:
    /// newer games first, because more players own them.
    /// </summary>
    public static IReadOnlyList<FromSoftwareGame> All { get; } =
    [
        new(1245620, "ELDEN RING", 0),
        new(2622380, "ELDEN RING NIGHTREIGN", 1),
        new(1888160, "ARMORED CORE VI FIRES OF RUBICON", 2),
        new(814380, "Sekiro: Shadows Die Twice", 3),
        new(374320, "DARK SOULS III", 4),
        new(570940, "DARK SOULS: REMASTERED", 5),
        new(335300, "DARK SOULS II: Scholar of the First Sin", 6),
        new(236430, "DARK SOULS II", 7),
        new(211420, "DARK SOULS: Prepare To Die Edition", 8),
    ];

    public static FromSoftwareGame? Find(uint appId) => All.FirstOrDefault(g => g.AppId == appId);
}
