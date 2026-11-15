using System.Collections.ObjectModel;
using HuntersAccord.Core.Input;
using HuntersAccord.Core.Patches;
using HuntersAccord.Core.Settings;
using HuntersAccord.Launcher.Localization;
using HuntersAccord.Launcher.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HuntersAccord.Launcher.ViewModels;

public sealed partial class PatchOptionViewModel : ObservableObject
{
    private readonly PatchesViewModel _owner;
    internal bool Loading;

    public PatchOptionViewModel(PatchEntry entry, PatchesViewModel owner)
    {
        Entry = entry;
        _owner = owner;
        Name = new LocText(entry.TextKey);
    }

    public PatchEntry Entry { get; }
    public LocText Name { get; }
    /// <summary>Description with the player's keys in place of pad buttons ("hold [E] and press [Space]").</summary>
    [ObservableProperty] public partial string Help { get; set; } = "";

    internal void UpdateHelp(IReadOnlyList<InputBinding> bindings, IReadOnlyDictionary<string, string> debugKeys) =>
        Help = KeyBindingActions.WithKeys(Localizer.Instance[Entry.TextKey + "Help"], bindings,
            pad => Localizer.Instance.Has("Pad." + pad) ? Localizer.Instance["Pad." + pad] : pad.ToUpperInvariant(), debugKeys)
            + (IsVSyncPatch ? " " + Localizer.Instance["Patches.NoVsyncEmulator"] : "");

    public bool IsFeatured => Entry.Source == PatchSource.HuntersAccord;

    /// <summary>"No VSync" changes the game; the emulator's own V-Sync (Settings → Display) is a different thing: tagged and pointed to.</summary>
    public bool IsVSyncPatch => Entry.Key == "no_vsync";

    public bool CanToggle => IsAvailable && _owner.IsEditable;

    internal void NotifyEditable() => OnPropertyChanged(nameof(CanToggle));

    partial void OnIsAvailableChanged(bool value) => NotifyEditable();

    /// <summary>The debug menu needs the debug font files (Nexus Mods): the row links to them and says if they are found.</summary>
    public bool NeedsDebugFont => Entry.Key == "debug_menu";

    [ObservableProperty] public partial bool DebugFontFound { get; set; }

    [RelayCommand]
    private void OpenDebugFontPage() => HuntersAccord.Platform.PlatformServices.Current.OpenUrl(DebugFont.DownloadUrl);

    [ObservableProperty] public partial bool IsOn { get; set; }

    [ObservableProperty] public partial bool IsAvailable { get; set; }

    public LocText UnavailableTag { get; } = new("Patches.NotAvailable");

    partial void OnIsOnChanged(bool value)
    {
        if (!Loading) _owner.Toggle(this, value);
    }
}

public sealed partial class PatchSectionViewModel(PatchSection section, IReadOnlyList<PatchOptionViewModel> patches) : ObservableObject
{
    public LocText Title { get; } = new(section.TextKey);
    public IReadOnlyList<PatchOptionViewModel> Patches => patches;

    public IReadOnlyList<PatchOptionViewModel> Rows { get; } = [.. patches.Where(p => !p.IsFeatured)];
    public bool DeveloperOnly => section.DeveloperOnly;

    public bool IsHuntersAccord { get; } = section.Patches.All(p => p.Source == PatchSource.HuntersAccord);

    public bool IsGraphics { get; } = section.TextKey == "PatchSection.Graphics";

    [ObservableProperty] public partial bool IsVisible { get; set; } = !section.DeveloperOnly;
}

public sealed class PatchRailHeading
{
    public LocText Title { get; } = new("Patches.CommunityHead");
}

/// <summary>Community patches (GoldHEN and shadPS4 repositories) that the mod applies when the game starts; the choice is stored in the game settings.</summary>
public sealed partial class PatchesViewModel : ObservableObject
{
    private const int DeveloperClicks = 5;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private int _developerClicks;

    public PatchesViewModel()
    {
        Sections = [.. PatchCatalog.Sections.Select(s => new PatchSectionViewModel(s, [.. s.Patches.Select(p => new PatchOptionViewModel(p, this))]))];
        Featured = All.First(p => p.IsFeatured);
        DeveloperMode = GameSettings.Load().DeveloperMode;
        foreach (var section in Sections.Where(s => s.IsVisible && !VisibleSections.Contains(s))) VisibleSections.Add(section);
        UpdateRail();
        SelectedSection = VisibleSections[0];
        ShowDeveloperMode = DeveloperMode;
        Localizer.Instance.LanguageChanged += (_, _) =>
        {
            UpdateSummary();
            RefreshKeys();
        };
        Load();
    }

    public IReadOnlyList<PatchSectionViewModel> Sections { get; }

    public ObservableCollection<PatchSectionViewModel> VisibleSections { get; } = [];

    public ObservableCollection<object> RailItems { get; } = [];

    private readonly PatchRailHeading _communityHeading = new();

    private void UpdateRail()
    {
        RailItems.Clear();
        foreach (var section in VisibleSections.Where(s => s.IsHuntersAccord)) RailItems.Add(section);
        RailItems.Add(_communityHeading);
        foreach (var section in VisibleSections.Where(s => !s.IsHuntersAccord)) RailItems.Add(section);
    }

    [ObservableProperty] public partial PatchSectionViewModel? SelectedSection { get; set; }

    public PatchOptionViewModel Featured { get; }

    public LocText FeaturedNote { get; } = new("Patches.TurnsOff", "");

    [ObservableProperty] public partial bool ShowFeaturedNote { get; set; }

    [ObservableProperty] public partial bool ShowFeaturedDetails { get; set; }

    [RelayCommand]
    private void OpenFeaturedDetails() => ShowFeaturedDetails = true;

    [RelayCommand]
    private void CloseFeaturedDetails() => ShowFeaturedDetails = false;

    [RelayCommand]
    private void TurnOnFeatured()
    {
        if (Featured.CanToggle) Featured.IsOn = true;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActive))]
    public partial IReadOnlyList<string> ActiveNames { get; set; } = [];

    public bool HasActive => ActiveNames.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMore))]
    public partial string ActiveMore { get; set; } = "";

    public bool HasMore => ActiveMore.Length > 0;

    [ObservableProperty]
    public partial string ActiveMoreNames { get; set; } = "";

    private IEnumerable<PatchOptionViewModel> All => Sections.SelectMany(s => s.Patches);

    public LocText FilesStatus { get; } = new("Patches.Missing");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFilesDate), nameof(ShowFilesWarning))]
    public partial bool FilesStatusIsWarning { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFilesDate), nameof(ShowFilesWarning), nameof(CanUpdate))]
    public partial bool IsUpdating { get; set; }

    public bool CanUpdate => IsEditable && !IsUpdating;

    public bool ShowFilesDate => !FilesStatusIsWarning && !IsUpdating;

    public bool ShowFilesWarning => FilesStatusIsWarning && !IsUpdating;

    public LocText Notice { get; } = new("Patches.FontInstalled");

    [ObservableProperty] public partial bool ShowNotice { get; set; }

    [ObservableProperty] public partial bool NoticeIsWarning { get; set; }

    private void SetNotice(bool warning, string key, params object?[] args)
    {
        Notice.Set(key, args);
        NoticeIsWarning = warning;
        ShowNotice = true;
    }

    /// <summary>False while the game runs: the mod keeps its own copy of the settings and would write it back.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocked), nameof(CanUpdate))]
    public partial bool IsEditable { get; set; } = true;

    public bool IsLocked => !IsEditable;

    partial void OnIsEditableChanged(bool value)
    {
        foreach (var option in All) option.NotifyEditable();
        HuntersAccord.IsEditable = value;
    }

    public HuntersAccordOptionsViewModel HuntersAccord { get; } = new();

    [ObservableProperty] public partial bool DeveloperMode { get; set; }

    [ObservableProperty] public partial bool ShowDeveloperMode { get; set; }

    public void CountDeveloperClick()
    {
        if (++_developerClicks >= DeveloperClicks) ShowDeveloperMode = true;
    }

    partial void OnDeveloperModeChanged(bool value)
    {
        foreach (var section in Sections.Where(s => s.DeveloperOnly)) section.IsVisible = value;
        var selected = SelectedSection;
        VisibleSections.Clear();
        foreach (var section in Sections.Where(s => s.IsVisible)) VisibleSections.Add(section);
        UpdateRail();
        SelectedSection = selected is not null && VisibleSections.Contains(selected) ? selected : VisibleSections[0];
        UpdateSummary();
        var settings = GameSettings.Load();
        if (settings.DeveloperMode == value) return;
        settings.DeveloperMode = value;
        settings.Save();
    }

    public IEnumerable<PatchEntry> Active() =>
        Sections.Where(s => s.IsVisible).SelectMany(s => s.Patches).Where(p => p.IsOn && p.IsAvailable).Select(p => p.Entry);

    /// <summary>Downloads the patch files from the repositories named by <see cref="PatchLibrary.SourceUrl"/>; only on Update, never on its own.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        ShowNotice = false;
        IsUpdating = true;
        Exception? failure;
        try
        {
            failure = await PatchLibrary.UpdateAsync(Http);
        }
        finally
        {
            IsUpdating = false;
        }
        Load();
        if (failure is null) return;
        // Offline or a weak connection: say so; the files already here (if any) stay in use.
        FilesStatusIsWarning = true;
        if (PatchLibrary.UpdatedAt() is { } at) FilesStatus.Set("Patches.UpdateFailed", FriendlyErrors.Describe(failure), at.ToString("d"));
        else FilesStatus.Set("Patches.MissingFailed", FriendlyErrors.Describe(failure));
    }

    public void InstallDebugFont(string source)
    {
        if (Game is not { } game)
        {
            SetNotice(true, "Patches.FontNoGame");
            return;
        }
        try
        {
            if (!DebugFont.Install(game, source))
            {
                SetNotice(true, "Patches.FontNotInFile");
                return;
            }
            SetNotice(false, "Patches.FontInstalled");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            SetNotice(true, "Patches.FontFailed", FriendlyErrors.Describe(e));
        }
        RefreshKeys();
    }

    public HuntersAccord.Core.Game.GameInstall? Game { get; set; }

    public void RefreshKeys()
    {
        var settings = GameSettings.Load();
        var bindings = settings.EffectiveBindings;
        var fontFound = Game is { } game && DebugFont.IsInstalled(game);
        foreach (var option in All)
        {
            option.UpdateHelp(bindings, settings.DebugKeys);
            option.DebugFontFound = fontFound;
        }
    }

    private void Load()
    {
        var enabled = GameSettings.Load().Patches;
        RefreshKeys();
        foreach (var option in All)
        {
            option.Loading = true;
            option.IsAvailable = PatchLibrary.Find(option.Entry) is not null;
            option.IsOn = enabled.Contains(option.Entry.Key);
            option.Loading = false;
        }
        DropConflicts();
        UpdateSummary();
        var updated = PatchLibrary.UpdatedAt();
        foreach (var option in All) option.UnavailableTag.Set(updated is null ? "Patches.NotDownloaded" : "Patches.NotAvailable");
        if (updated is { } at) FilesStatus.Set("Patches.FilesFrom", at.ToString("d"));
        else FilesStatus.Set("Patches.Missing");
        FilesStatusIsWarning = updated is null;
    }

    /// <summary>A patch was switched: save it, and turn off patches that change the same code (only one can apply).</summary>
    internal void Toggle(PatchOptionViewModel option, bool on)
    {
        var settings = GameSettings.Load();
        settings.SetPatch(option.Entry.Key, on);
        if (on)
        {
            foreach (var other in All.Where(o => o.IsOn && PatchLibrary.Conflict(option.Entry, o.Entry)))
            {
                other.Loading = true;
                other.IsOn = false;
                other.Loading = false;
                settings.SetPatch(other.Entry.Key, false);
                SetNotice(true, "Patches.Replaced", other.Name, option.Name);
            }
        }
        settings.Save();
        UpdateSummary();
    }

    private void DropConflicts()
    {
        var on = All.Where(o => o.IsOn).ToList();
        var dropped = on.Where((o, i) => on.Take(i).Any(k => k.IsOn && PatchLibrary.Conflict(k.Entry, o.Entry))).ToList();
        if (dropped.Count == 0) return;
        var settings = GameSettings.Load();
        foreach (var option in dropped)
        {
            option.Loading = true;
            option.IsOn = false;
            option.Loading = false;
            settings.SetPatch(option.Entry.Key, false);
        }
        settings.Save();
    }

    /// <summary>Most names the main window's column lists; the rest collapse into "and N more".</summary>
    internal const int ShownNames = 7;

    private void UpdateSummary()
    {
        // The featured patch first, then the others in the order of the sections.
        var names = Active().OrderBy(e => e != Featured.Entry).Select(e => Localizer.Instance.Format(e.TextKey)).ToList();
        ActiveNames = [.. names.Take(ShownNames)];
        ActiveMore = names.Count > ShownNames ? Localizer.Instance.Format("Patches.More", names.Count - ShownNames) : "";
        ActiveMoreNames = string.Join('\n', names.Skip(ShownNames));
        // The card says which patches turning it on would turn off (No VSync, Performance Patch).
        var replaced = All.Where(o => o.IsOn && !Featured.IsOn && PatchLibrary.Conflict(Featured.Entry, o.Entry)).Select(o => o.Name.Text).ToList();
        FeaturedNote.Set("Patches.TurnsOff", string.Join(", ", replaced));
        ShowFeaturedNote = replaced.Count > 0;
    }
}
