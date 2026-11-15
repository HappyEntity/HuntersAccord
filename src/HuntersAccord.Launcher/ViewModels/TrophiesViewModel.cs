using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HuntersAccord.Core;
using HuntersAccord.Core.Game;
using HuntersAccord.Core.Trophies;
using HuntersAccord.Launcher.Localization;
using HuntersAccord.Launcher.Services;

namespace HuntersAccord.Launcher.ViewModels;

/// <summary>What the list shows by whether a trophy is held (one at a time).</summary>
public enum TrophyShow
{
    All,
    Earned,
    NotEarned,
}

/// <summary>The list's order.</summary>
public enum TrophySort
{
    Game,
    Date,
    Rank,
}

/// <summary>A choice in the filter column: its words, its count, whether it is on.</summary>
public sealed partial class TrophyFilter(string key, IImage? badge = null) : ObservableObject
{
    public LocText Label { get; } = new(key);

    public IImage? Badge { get; } = badge;

    public bool HasBadge => Badge is not null;

    [ObservableProperty] public partial int Count { get; set; }

    [ObservableProperty] public partial bool IsActive { get; set; }
}

/// <summary>A sort order in the list's select.</summary>
public sealed record TrophySortOption(TrophySort Sort, LocText Label);

/// <summary>
/// One trophy's row: its picture (or rank badge), name, description, and when / with whom / how it was earned. A hidden
/// trophy not yet earned shows "???" until the player clicks it (<see cref="Reveal"/>), for as long as the launcher runs.
/// </summary>
public sealed partial class TrophyRowViewModel : ObservableObject
{
    private readonly string _name, _description;
    private readonly IImage _openBadge;
    private readonly Action<int>? _revealed;

    public TrophyRowViewModel(TrophyRow row, IImage? picture, string language, bool revealed = false, Action<int>? onReveal = null)
    {
        Row = row;
        Picture = picture;
        var info = row.Info;
        IsEarned = row.IsEarned;
        IsHiddenTrophy = row.IsSecret;
        IsDlc = info.Dlc;
        _name = info.Name(language);
        _description = info.Description(language);
        _openBadge = TrophyArt.Badge(info.Grade, IsEarned, false);
        _secretBadge = TrophyArt.Badge(info.Grade, IsEarned, true);
        _revealed = onReveal;
        Revealed = revealed;
        if (row.Earned is { } e)
        {
            FromSave = e.FromSave;
            Date = e.Earned is { } at ? TrophyArt.Date(at) : null;
            Partner = e.Partner is { Length: > 0 } p ? Localizer.Instance.Format("Trophies.Coop", p) : null;
        }
    }

    public TrophyRow Row { get; }

    /// <summary>The trophy's own picture from the player's game copy (null: the rank badge alone).</summary>
    public IImage? Picture { get; }

    public bool HasPicture => Picture is not null && !IsSecret;

    private readonly IImage _secretBadge;

    public IImage Badge => IsSecret ? _secretBadge : _openBadge;

    public bool IsEarned { get; }

    public bool IsLocked => !IsEarned;

    /// <summary>Hidden and not earned: shown as "???" until revealed.</summary>
    public bool IsHiddenTrophy { get; }

    /// <summary>Clicked open in this launcher run.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSecret), nameof(Name), nameof(Description), nameof(Badge), nameof(HasPicture))]
    public partial bool Revealed { get; set; }

    public bool IsSecret => IsHiddenTrophy && !Revealed;

    public void Reveal()
    {
        if (!IsSecret) return;
        Revealed = true;
        _revealed?.Invoke(Row.Info.Id);
    }

    public bool IsDlc { get; }

    public string Name => IsSecret ? "???" : _name;

    public string Description => IsSecret ? Localizer.Instance["Trophies.Hidden"] : _description;

    public string? Date { get; }

    public bool HasDate => Date is not null;

    public bool FromSave { get; }

    public string? Partner { get; }

    public bool HasPartner => Partner is not null;
}

/// <summary>
/// The Trophies tab and the Play tab's widget: the player's own trophies (<see cref="TrophyService"/>, followed while the
/// launcher runs) filtered, sorted and counted; the trophies' pictures from the player's game copy
/// (<see cref="TrophyPictures"/>), the rank badges where there are none.
/// </summary>
public sealed partial class TrophiesViewModel : ObservableObject, IDisposable
{
    private readonly TrophyService _service;
    private readonly Dictionary<int, IImage> _pictures = [];

    /// <summary>Hidden trophies the player clicked open: kept while the launcher runs, never saved.</summary>
    private readonly HashSet<int> _revealed = [];
    private GameInstall? _game;

    public TrophiesViewModel(TrophyService service)
    {
        _service = service;
        ShowAll = new TrophyFilter("Trophies.All") { IsActive = true };
        ShowEarned = new TrophyFilter("Trophies.Earned");
        ShowNotEarned = new TrophyFilter("Trophies.NotEarned");
        Platinum = new TrophyFilter("Trophies.Rank.Platinum", TrophyArt.Badge(TrophyGrade.Platinum, true, false));
        Gold = new TrophyFilter("Trophies.Rank.Gold", TrophyArt.Badge(TrophyGrade.Gold, true, false));
        Silver = new TrophyFilter("Trophies.Rank.Silver", TrophyArt.Badge(TrophyGrade.Silver, true, false));
        Bronze = new TrophyFilter("Trophies.Rank.Bronze", TrophyArt.Badge(TrophyGrade.Bronze, true, false));
        BaseGame = new TrophyFilter("Trophies.Part.Base");
        OldHunters = new TrophyFilter("Trophies.Part.Dlc");
        SortOptions =
        [
            new(TrophySort.Game, new LocText("Trophies.Sort.Game")),
            new(TrophySort.Date, new LocText("Trophies.Sort.Date")),
            new(TrophySort.Rank, new LocText("Trophies.Sort.Rank")),
        ];
        SelectedSort = SortOptions[0];
        _service.Changed += (_, _) => Dispatcher.UIThread.Post(Refresh);
        Localizer.Instance.LanguageChanged += (_, _) => Refresh();
        try
        {
            _service.Watch();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Not followed: read again each time the tab opens.
        }
        Refresh();
    }

    public TrophyFilter ShowAll { get; }
    public TrophyFilter ShowEarned { get; }
    public TrophyFilter ShowNotEarned { get; }
    public TrophyFilter Platinum { get; }
    public TrophyFilter Gold { get; }
    public TrophyFilter Silver { get; }
    public TrophyFilter Bronze { get; }
    public TrophyFilter BaseGame { get; }
    public TrophyFilter OldHunters { get; }

    public IReadOnlyList<TrophySortOption> SortOptions { get; }

    [ObservableProperty] public partial TrophySortOption SelectedSort { get; set; }

    partial void OnSelectedSortChanged(TrophySortOption value) => Refresh();

    public ObservableCollection<TrophyRowViewModel> Rows { get; } = [];

    [ObservableProperty] public partial int EarnedCount { get; set; }
    [ObservableProperty] public partial int TotalCount { get; set; }
    [ObservableProperty] public partial double Percent { get; set; }
    [ObservableProperty] public partial string PercentText { get; set; } = "0 %";
    [ObservableProperty] public partial string OfText { get; set; } = "";
    [ObservableProperty] public partial string ShortCount { get; set; } = "";
    [ObservableProperty] public partial string ListCount { get; set; } = "";
    [ObservableProperty] public partial string GoldCount { get; set; } = "";
    [ObservableProperty] public partial string SilverCount { get; set; } = "";
    [ObservableProperty] public partial string BronzeCount { get; set; } = "";
    [ObservableProperty] public partial string GoldHeld { get; set; } = "0";
    [ObservableProperty] public partial string SilverHeld { get; set; } = "0";
    [ObservableProperty] public partial string BronzeHeld { get; set; } = "0";

    /// <summary>The platinum: held, its title, its state line, its badge (or picture).</summary>
    [ObservableProperty] public partial bool HasPlatinum { get; set; }
    [ObservableProperty] public partial string PlatinumName { get; set; } = "";
    [ObservableProperty] public partial string PlatinumState { get; set; } = "";
    [ObservableProperty] public partial IImage? PlatinumBadge { get; set; }
    [ObservableProperty] public partial IImage? PlatinumPicture { get; set; }

    /// <summary>No trophy held at all (the first start): the empty page instead of the list.</summary>
    [ObservableProperty] public partial bool IsEmpty { get; set; }

    /// <summary>Trophies held, but none passes the filters.</summary>
    [ObservableProperty] public partial bool NoneMatch { get; set; }

    /// <summary>The widget on Play: the last one's picture or badge, its name.</summary>
    [ObservableProperty] public partial IImage? LastBadge { get; set; }
    [ObservableProperty] public partial IImage? LastPicture { get; set; }
    [ObservableProperty] public partial string? LastText { get; set; }
    [ObservableProperty] public partial bool HasLast { get; set; }

    /// <summary>The game copy the pictures come from (the update's pack first): read in the background, then shown.</summary>
    public void UseGame(GameInstall? game)
    {
        if (game == _game) return;
        _game = game;
        var pack = game is null ? null : TrophyPack.Find(game.BasePath, game.UpdatePath);
        _ = Task.Run(() => TrophyPictures.Ensure(AppPaths.DataRoot, pack)).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
        {
            _pictures.Clear();
            Refresh();
        }), TaskScheduler.Default);
    }

    /// <summary>A trophy's picture (decoded once, small): null when the game copy gave none.</summary>
    private IImage? PictureOf(int id)
    {
        if (_pictures.TryGetValue(id, out var cached)) return cached;
        if (TrophyPictures.Find(AppPaths.DataRoot, id) is not { } file) return null;
        try
        {
            using var stream = File.OpenRead(file);
            var bitmap = Bitmap.DecodeToWidth(stream, 112, BitmapInterpolationMode.HighQuality);
            _pictures[id] = bitmap;
            return bitmap;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    [RelayCommand]
    private void Show(TrophyFilter filter)
    {
        foreach (var f in new[] { ShowAll, ShowEarned, ShowNotEarned }) f.IsActive = f == filter;
        Refresh();
    }

    /// <summary>A rank or a part on or off (several at once; none on: all).</summary>
    [RelayCommand]
    private void Toggle(TrophyFilter filter)
    {
        filter.IsActive = !filter.IsActive;
        Refresh();
    }

    public void Refresh()
    {
        var language = Localizer.Instance.Language;
        var all = _service.Rows();
        var progress = _service.Progress();

        EarnedCount = progress.Earned;
        TotalCount = progress.Total;
        Percent = progress.Percent;
        PercentText = $"{Math.Floor(progress.Percent):0} %";
        OfText = Localizer.Instance.Format("Trophies.Of", progress.Earned, progress.Total);
        ShortCount = $"{progress.Earned} / {progress.Total}";
        int Of(TrophyGrade g) => all.Count(r => r.Info.Grade == g);
        GoldHeld = progress.Gold.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SilverHeld = progress.Silver.ToString(System.Globalization.CultureInfo.InvariantCulture);
        BronzeHeld = progress.Bronze.ToString(System.Globalization.CultureInfo.InvariantCulture);
        GoldCount = $"/ {Of(TrophyGrade.Gold)}";
        SilverCount = $"/ {Of(TrophyGrade.Silver)}";
        BronzeCount = $"/ {Of(TrophyGrade.Bronze)}";

        var platinum = all.First(r => r.Info.Grade == TrophyGrade.Platinum);
        HasPlatinum = platinum.IsEarned;
        PlatinumName = platinum.Info.Name(language);
        PlatinumBadge = TrophyArt.Badge(TrophyGrade.Platinum, platinum.IsEarned, false);
        PlatinumPicture = PictureOf(platinum.Info.Id);
        PlatinumState = platinum.Earned switch
        {
            null => Localizer.Instance["Trophies.NotYet"],
            { Earned: { } at } => Localizer.Instance.Format("Trophies.EarnedOn", at.LocalDateTime.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture)),
            _ => Localizer.Instance["Trophies.FromSave"],
        };

        // The widget: the last one earned with a date (else the last of the save's).
        var last = all.Where(r => r.Earned?.Earned is not null).MaxBy(r => r.Earned!.Earned)
                   ?? all.LastOrDefault(r => r.IsEarned);
        HasLast = last is not null;
        LastText = last is null ? null : Localizer.Instance.Format("Trophies.Last", last.Info.Name(language));
        LastBadge = last is null ? TrophyArt.Hidden : TrophyArt.Badge(last.Info.Grade, true, false);
        LastPicture = last is null ? null : PictureOf(last.Info.Id);

        // The filter column's counts (each over the whole list, as the design shows them).
        ShowAll.Count = all.Count;
        ShowEarned.Count = all.Count(r => r.IsEarned);
        ShowNotEarned.Count = all.Count(r => !r.IsEarned);
        Platinum.Count = Of(TrophyGrade.Platinum);
        Gold.Count = Of(TrophyGrade.Gold);
        Silver.Count = Of(TrophyGrade.Silver);
        Bronze.Count = Of(TrophyGrade.Bronze);
        BaseGame.Count = all.Count(r => !r.Info.Dlc);
        OldHunters.Count = all.Count(r => r.Info.Dlc);

        var ranks = new (TrophyFilter Filter, TrophyGrade Grade)[] { (Platinum, TrophyGrade.Platinum), (Gold, TrophyGrade.Gold), (Silver, TrophyGrade.Silver), (Bronze, TrophyGrade.Bronze) };
        var rankOn = ranks.Where(r => r.Filter.IsActive).Select(r => r.Grade).ToHashSet();
        IEnumerable<TrophyRow> shown = all;
        if (ShowEarned.IsActive) shown = shown.Where(r => r.IsEarned);
        if (ShowNotEarned.IsActive) shown = shown.Where(r => !r.IsEarned);
        if (rankOn.Count > 0) shown = shown.Where(r => rankOn.Contains(r.Info.Grade));
        if (BaseGame.IsActive != OldHunters.IsActive) shown = shown.Where(r => r.Info.Dlc == OldHunters.IsActive);
        shown = SelectedSort.Sort switch
        {
            // Earned first, the newest on top; the save's (no date) after them; the rest in the game's order.
            TrophySort.Date => shown.OrderBy(r => r.IsEarned ? 0 : 1).ThenByDescending(r => r.Earned?.Earned ?? DateTimeOffset.MinValue),
            TrophySort.Rank => shown.OrderByDescending(r => r.Info.Grade).ThenBy(r => r.Info.Id),
            _ => shown,
        };
        var list = shown.ToList();
        Rows.Clear();
        foreach (var row in list) Rows.Add(new TrophyRowViewModel(row, PictureOf(row.Info.Id), language, _revealed.Contains(row.Info.Id), id => _revealed.Add(id)));
        ListCount = Localizer.Instance.Format("Trophies.Count", list.Count);
        // Nothing earned yet: the list still shows what there is to earn; the note only stands in for an empty list.
        IsEmpty = progress.Earned == 0 && list.Count == 0;
        NoneMatch = !IsEmpty && list.Count == 0;
    }

    public void Dispose() => _service.Dispose();
}

/// <summary>The rank badges (Theme/TrophyBadges.axaml) and the trophies' date.</summary>
public static class TrophyArt
{
    private static IImage Resource(string key) =>
        Application.Current?.TryGetResource(key, null, out var value) == true && value is IImage image ? image : EmptyImage.Instance;

    public static IImage Hidden => Resource("TrophyHidden");

    public static IImage Badge(TrophyGrade grade, bool earned, bool secret) =>
        secret ? Hidden : Resource("Trophy" + grade + (earned ? "" : "Locked"));

    public static string Date(DateTimeOffset at) => at.LocalDateTime.ToString("dd.MM.yyyy · HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private sealed class EmptyImage : IImage
    {
        public static readonly EmptyImage Instance = new();
        public Size Size => new(48, 48);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) { }
    }
}
