using HuntersAccord.Core.Game;
using HuntersAccord.Core.Trophies;

namespace HuntersAccord.Launcher.Services;

/// <summary>
/// The trophies' words the launcher shows (the Trophies tab, the Play widget, the in-game notices): the game's own, in the
/// game's language, when the player's trophy key opens their game copy (<see cref="OfficialTrophies"/>), else Hunters'
/// Accord's own list (<see cref="TrophyCatalog"/>). The game's texts stay in memory here: never written, logged or sent
/// (the notices drawn from them are pictures in the data folder, like the rest of their cache).
/// </summary>
public sealed class TrophyTextSource
{
    public static TrophyTextSource Instance { get; } = new();

    private string _stamp = Own;
    private const string Own = "own";

    /// <summary>The words changed (key, game copy or language): on the caller's thread.</summary>
    public event EventHandler? Changed;

    /// <summary>The game's texts in use; null: our own list.</summary>
    public IReadOnlyDictionary<int, OfficialTrophy>? Texts { get; private set; }

    public bool IsOfficial => Texts is not null;

    /// <summary>
    /// What the words are, for the notices' stamp: "own", or the game's in a language from a pack. Never the key, never a
    /// text.
    /// </summary>
    public string Stamp => _stamp;

    /// <summary>Our list's entries with the game's words applied (when they are in use).</summary>
    public IReadOnlyList<TrophyInfo> All => Texts is { } texts ? [.. TrophyCatalog.All.Select(t => OfficialTrophies.Apply(t, texts))] : TrophyCatalog.All;

    public TrophyInfo Apply(TrophyInfo info) => OfficialTrophies.Apply(info, Texts);

    /// <summary>The words for a game copy, a checked key (null: none valid) and a PS4 language; <see cref="Changed"/> when they change.</summary>
    public void Update(GameInstall? game, byte[]? validKey, int language)
    {
        IReadOnlyDictionary<int, OfficialTrophy>? texts = null;
        var stamp = Own;
        if (game is not null && validKey is not null
            && TrophyPack.Find(game.BasePath, game.UpdatePath) is { } path && TrophyPack.Open(path) is { } pack
            && TrophyKey.CommIdOf(game.BasePath, game.UpdatePath, TitleIdOf(game)) is { } commId)
        {
            texts = OfficialTrophies.Read(pack, validKey, commId, language);
            if (texts is not null) stamp = $"official-{OfficialTrophies.ListName(language)}-{new FileInfo(path).Length}";
        }
        var changed = stamp != _stamp;
        Texts = texts;
        _stamp = stamp;
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The title id of a copy: its base folder's name when it is one (CUSA…), else the supported title's.</summary>
    public static string TitleIdOf(GameInstall game)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(game.BasePath));
        return name.Length == 9 && name.StartsWith("CUSA", StringComparison.OrdinalIgnoreCase) ? name.ToUpperInvariant() : Bloodborne.TitleId;
    }
}
