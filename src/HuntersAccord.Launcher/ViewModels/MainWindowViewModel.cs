using System.Collections.ObjectModel;
using HuntersAccord.Core.Emulator;
using HuntersAccord.Core.Game;
using HuntersAccord.Core.Settings;
using HuntersAccord.Core.Steam;
using HuntersAccord.Core.Updates;
using HuntersAccord.Launcher.Localization;
using HuntersAccord.Launcher.Services;
using HuntersAccord.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HuntersAccord.Launcher.ViewModels;

public sealed record ChannelOption(EmulatorChannel Value, LocText Label);

/// <summary>Game (PS4 system) language choice; <see cref="Id"/> null = follow the launcher language.</summary>
public sealed record GameLanguageOption(int? Id, LocText Label);

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ShadPs4Downloader _downloader = new(ShadPs4Downloader.CreateHttpClient());
    private readonly LauncherSettings _settings;

    public MainWindowViewModel(LauncherSettings settings, IStorageService storage)
    {
        _settings = settings;
        ModLibrary.Find()?.StartSessionDirectory(); // co-op sessions of the game instances started from here
        Steam = new SteamViewModel(settings, storage) { ShowDeveloperTools = Patches.DeveloperMode };
        Patches.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(PatchesViewModel.DeveloperMode)) return;
            OnPropertyChanged(nameof(ShowDeveloperTools));
            Steam.ShowDeveloperTools = ShowDeveloperTools;
        };
        Game = new GameSectionViewModel(settings, storage);
        Trophies = new TrophiesViewModel(new TrophyService());
        Trophies.UseGame(Game.SelectedInstall?.Install);
        Display = new DisplayViewModel(settings);
        Controls = new ControlsViewModel(settings);
        SelectedLanguage = Languages.FirstOrDefault(l => l.Code == Localizer.Instance.Language) ?? Languages[0];
        SelectedChannel = Channels.First(c => c.Value == settings.EmulatorChannel);
        SelectedGameLanguage = GameLanguages.FirstOrDefault(l => l.Id == settings.GameLanguage) ?? GameLanguages[0];

        // The trophies' words: the game's own (trophy key, game copy, game language) or ours; when they change, the list
        // and the in-game notices follow.
        TrophyTextSource.Instance.Changed += (_, _) =>
        {
            Trophies.Refresh();
            DrawToastsAhead();
        };
        Localizer.Instance.LanguageChanged += (_, _) => UpdateTrophyTexts();
        Game.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameSectionViewModel.SelectedInstall))
            {
                Trophies.UseGame(Game.SelectedInstall?.Install);
                UpdateTrophyTexts();
                DrawToastsAhead();
            }
            if (e.PropertyName == nameof(GameSectionViewModel.ValidTrophyKey)) UpdateTrophyTexts();
            if (e.PropertyName is nameof(GameSectionViewModel.IsReady) or nameof(GameSectionViewModel.IsInstalling)
                or nameof(GameSectionViewModel.IsScanning))
            {
                PlayCommand.NotifyCanExecuteChanged();
                PlayTwoPlayersCommand.NotifyCanExecuteChanged();
                UpdateHome();
            }
        };
    }


    /// <summary>The in-game notices drawn ahead (once per language, game copy and words), so Play does not wait for them.</summary>
    private void DrawToastsAhead()
    {
        if (Game.SelectedInstall?.Install is { } copy && GameLauncher.FindModLayerDir() is not null)
            Avalonia.Threading.Dispatcher.UIThread.Post(() => TrophyToastRenderer.Ensure(Localizer.Instance.Language, copy),
                Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>The PS4 language of the game's trophy texts: the game language chosen, else the launcher's.</summary>
    private int TrophyLanguage => _settings.GameLanguage ?? ConsoleLanguages.ForUiLanguage(Localizer.Instance.Language);

    private void UpdateTrophyTexts() =>
        TrophyTextSource.Instance.Update(Game.SelectedInstall?.Install, Game.ValidTrophyKey, TrophyLanguage);

    private readonly EmulatorProfile _profile = EmulatorProfile.Default;

    public LocText PlayStatus { get; } = new("Play.Idle");

    private bool _shaderCacheNoticeShown;

    [ObservableProperty] public partial bool HasPlayError { get; set; }

    /// <summary>Troubleshooting: start plain shadPS4 (no Hunters' Accord layer, no IPC) with the same profile. Not saved.</summary>
    [ObservableProperty] public partial bool LaunchWithoutMod { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand), nameof(PlayTwoPlayersCommand))]
    public partial bool IsGameRunning { get; set; }

    partial void OnIsGameRunningChanged(bool value)
    {
        Patches.IsEditable = !value;
        Display.IsGameRunning = value;
        Controls.IsGameRunning = value;
        UpdateHome();
    }

    public DisplayViewModel Display { get; }

    public ControlsViewModel Controls { get; }

    public SteamViewModel Steam { get; }

    public PatchesViewModel Patches { get; } = new();

    public BackupsViewModel Backups { get; } = new();

    /// <summary>The player's own trophies (the Trophies tab, the widget on Play).</summary>
    public TrophiesViewModel Trophies { get; }


    public LocText PlayLabel { get; } = new("Play.Button");

    public LocText HomeStatus { get; } = new("Home.Searching");

    [ObservableProperty] public partial bool ShowPanel { get; set; }
    [ObservableProperty] public partial bool ShowHomeStatus { get; set; } = true;
    [ObservableProperty] public partial bool ShowNoModStamp { get; set; }

    [ObservableProperty] public partial bool IsDownloadingEmulator { get; set; }

    partial void OnLaunchWithoutModChanged(bool value) => UpdateHome();
    partial void OnHasPlayErrorChanged(bool value) => UpdateHome();
    partial void OnEmulatorPathChanged(string? value)
    {
        UpdateHome();
        PlayTwoPlayersCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        UpdateHome();
        PlayTwoPlayersCommand.NotifyCanExecuteChanged();
    }
    partial void OnDownloadProgressChanged(double value)
    {
        if (IsDownloadingEmulator || IsBusy) UpdateHome();
    }

    private void UpdateHome()
    {
        ShowPanel = !Game.IsScanning && (!Game.IsReady || Game.IsInstalling);
        PlayLabel.Set(IsGameRunning ? "Play.RunningButton" : "Play.Button");
        ShowNoModStamp = !HasPlayError && LaunchWithoutMod && !IsGameRunning && !IsDownloadingEmulator;
        ShowHomeStatus = !HasPlayError && !ShowNoModStamp && !IsGameRunning;

        if (Game.IsScanning) HomeStatus.Set("Home.Searching");
        else if (IsDownloadingEmulator || (IsBusy && DownloadProgress > 0)) HomeStatus.Set("Home.Downloading", DownloadProgress);
        else if (IsBusy) HomeStatus.Set("Home.Querying");
        else if (EmulatorPath is not null)
            HomeStatus.Set("Home.Ready", _installed is { } local ? ShadPs4Release.DisplayNameOf(local.Tag) : "shadPS4");
        else HomeStatus.Set("Home.NoEmulator");
    }

    private bool CanPlay() => !IsGameRunning && !IsBusy && !Game.IsInstalling && Game.IsReady;

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task PlayAsync()
    {
        if (Game.SelectedInstall?.Install is not { } game) return;
        HasPlayError = false;
        // Without shadPS4 the player is asked first (what, from where, how big); only on Download is it downloaded.
        if (EmulatorPath is null && !await DownloadWithConsentAsync()) return;
        if (EmulatorPath is null) return;
        _profile.EnsureCreated(seedFromUserDir: PlatformServices.Current.GetShadPs4UserDir());
        IsGameRunning = true;
        try
        {
            // Co-op over Steam unless only this PC's players are wanted (developer mode) or the mod is off.
            await RunGameAsync(game, _profile, player: 1, LaunchWithoutMod, steam: LaunchWithoutMod ? null : Steam.Launch());
        }
        finally
        {
            IsGameRunning = false;
        }
    }

    /// <summary>Starts the game with <paramref name="profile"/> and waits until it closes; failures go to the status line.</summary>
    /// <param name="half">Where its window goes (0 left, 1 right half of the screen), or null to leave it where it opens.</param>
    /// <param name="steam">Co-op over Steam; null: with this PC's players only.</param>
    private async Task RunGameAsync(GameInstall game, EmulatorProfile profile, int player, bool withoutMod, int? half = null,
        SteamLaunch? steam = null)
    {
        if (EmulatorPath is not { } emulator) return;
        try
        {
            profile.SetConsoleLanguage(_settings.GameLanguage ?? ConsoleLanguages.ForUiLanguage(Localizer.Instance.Language));
            profile.ApplyDisplay(_settings.Display, windowed: half is not null); // side by side: always windows
            // The game files Hunters' Accord overrides are the mod's to make (ModLibrary: the mod library in mod/). Without
            // it (a launcher built from the public source alone) the game starts as shadPS4 alone would.
            var mod = ModLibrary.Find();
            var modLayer = withoutMod ? null : GameLauncher.FindModLayerDir();
            if (modLayer is not null && mod is null)
            {
                // The layer is there but its library does not load (damaged, or from another launcher version).
                PlayStatus.Set("Play.ModLibraryFailed", ModLibrary.FileName);
                HasPlayError = true;
                return;
            }
            if (modLayer is not null && mod is not null)
            {
                var texts = Localizer.Instance.AllTables();
                var patches = Patches.Active().Select(p => p.Key).ToList();
                var freeBells = Patches.DeveloperMode;
                var gameSettings = GameSettings.Load(); // fe.gfx and the logo screens follow the Hunters' Accord section
                await Task.Run(() => mod.BuildModFiles(game, texts, patches, freeBells, gameSettings.ShowKeyPrompts, gameSettings.SkipLogos));
                // The trophies' in-game notices in the launcher's language (drawn once per language, here on the UI thread).
                TrophyToastRenderer.Ensure(Localizer.Instance.Language, game);
            }
            else if (mod is not null)
                await Task.Run(() => mod.SetModFilesEnabled(game, false)); // the game's own menu files, not Hunters' Accord's
            var startedAt = DateTime.Now;
            using var session = EmulatorSession.Start(emulator, game, profile, modLayer, player, _settings.HideEmulatorConsole,
                _settings.Display.HotkeysOff, steam, _settings.Gamepad, _settings.Display.Resolution);
            var process = session.Process;
            if (half is { } side) _ = Task.Run(() => GameWindows.PlaceHalf(process, side, _settings.Display.Resolution.Aspect));
            // The shader cache was deleted before this start: the first minutes stutter while it is built again.
            if (profile.LastShaderCacheReset is var reset and not ShaderCacheReset.None)
            {
                PlayStatus.Set("Play.ShaderCacheRebuilt", new LocText(reset == ShaderCacheReset.GpuOrDriverChanged
                    ? "Play.ShaderCacheRebuilt.Driver" : "Play.ShaderCacheRebuilt.Emulator"));
                _shaderCacheNoticeShown = true;
            }
            else if (_shaderCacheNoticeShown)
            {
                PlayStatus.Set("Play.Idle"); // the notice was about an earlier start
                _shaderCacheNoticeShown = false;
            }
            // The render size goes in at the game's very start; on a slow start the mod can come too late for it.
            if (modLayer is not null && !_settings.Display.Resolution.IsStock) _ = WatchResolutionAsync(process, player, startedAt);

            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                PlayStatus.Set("Play.Crashed", $"0x{process.ExitCode:X8}", profile.LogFile);
                HasPlayError = true;
            }
        }
        catch (Exception e)
        {
            PlayStatus.Set("Play.StartFailed", FriendlyErrors.Describe(e));
            HasPlayError = true;
        }
    }

    /// <summary>Warns when the game stays at 1920x1080: the mod came in after the game had taken its size, or the size could not be set.</summary>
    private async Task WatchResolutionAsync(System.Diagnostics.Process process, int player, DateTime since)
    {
        for (var second = 0; second < 120 && !process.HasExited; second++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            if (ResolutionLastStart.Read(player, since) is not { } last) continue;
            if (!last.Applied)
                PlayStatus.Set(last.Reason == ResolutionLastStart.TooLate ? "Play.ResolutionLate" : "Play.ResolutionStock",
                    last.Requested.ToString());
            return;
        }
    }


    private const int SecondPlayer = 2;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayTwoPlayersCommand))]
    public partial bool IsSecondPlayerRunning { get; set; }

    public bool ShowDeveloperTools => Patches.DeveloperMode;

    private bool CanPlayTwoPlayers() =>
        !IsGameRunning && !IsSecondPlayerRunning && !IsBusy && !Game.IsInstalling && Game.IsReady && EmulatorPath is not null;

    /// <summary>Starts both players side by side: the main profile on the left, a second profile (copied from the main one the first time) on the right.</summary>
    [RelayCommand(CanExecute = nameof(CanPlayTwoPlayers))]
    private async Task PlayTwoPlayersAsync()
    {
        if (Game.SelectedInstall?.Install is not { } game) return;
        HasPlayError = false;
        _profile.EnsureCreated(seedFromUserDir: PlatformServices.Current.GetShadPs4UserDir());
        var profile = EmulatorProfile.ForPlayer(SecondPlayer);
        try
        {
            await Task.Run(() => profile.EnsureCopiedFrom(_profile));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            PlayStatus.Set("Play.StartFailed", FriendlyErrors.Describe(e));
            HasPlayError = true;
            return;
        }
        IsGameRunning = true;
        IsSecondPlayerRunning = true;
        try
        {
            var first = RunGameAsync(game, _profile, player: 1, withoutMod: false, half: 0);
            await Task.Delay(TimeSpan.FromSeconds(3)); // one at a time
            await Task.WhenAll(first, RunGameAsync(game, profile, SecondPlayer, withoutMod: false, half: 1));
        }
        finally
        {
            IsGameRunning = false;
            IsSecondPlayerRunning = false;
        }
    }

    /// <summary>The profile folder "Profile folder" opens (shown beside the button).</summary>
    public string ProfileFolder => _profile.UserDir;

    [RelayCommand]
    private void OpenProfileFolder()
    {
        _profile.EnsureCreated(seedFromUserDir: PlatformServices.Current.GetShadPs4UserDir());
        PlatformServices.Current.OpenFolder(_profile.UserDir);
    }

    public GameSectionViewModel Game { get; }

    public IReadOnlyList<GameLanguageOption> GameLanguages { get; } =
    [
        new(null, new LocText("GameLang.Auto")),
        .. ConsoleLanguages.All.Select(l => new GameLanguageOption(l.Id, new LocText("GameLang.Name", l.NativeName))),
    ];

    [ObservableProperty] public partial GameLanguageOption SelectedGameLanguage { get; set; }

    partial void OnSelectedGameLanguageChanged(GameLanguageOption value)
    {
        if (_settings.GameLanguage == value.Id) return;
        _settings.GameLanguage = value.Id;
        SettingsStore.Save(_settings);
        UpdateTrophyTexts(); // the game's trophy texts follow the game's language
    }


    private static readonly string VersionNumber = typeof(MainWindowViewModel).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion
        .Split('+')[0] ?? "";

    public string AppVersion { get; } = "Hunters’ Accord " + VersionNumber;

    public string AppCopyright { get; } = typeof(MainWindowViewModel).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyCopyrightAttribute), false)
        .OfType<System.Reflection.AssemblyCopyrightAttribute>().FirstOrDefault()?.Copyright ?? "";

    public string AppAbout => VersionNumber + " · " + AppCopyright;

    [RelayCommand]
    private void OpenUrl(string url) => PlatformServices.Current.OpenUrl(url);

    [RelayCommand]
    private void OpenLicenses()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "licenses");
        if (Directory.Exists(dir)) PlatformServices.Current.OpenFolder(dir);
    }


    /// <summary>A newer release exists in the public repository (it is never downloaded).</summary>
    public LocText UpdateNotice { get; } = new("Home.UpdateAvailable", "");

    [ObservableProperty] public partial bool HasUpdate { get; set; }

    private string _updatePage = UpdateCheck.ReleasesPage;

    [RelayCommand]
    private void OpenUpdatePage() => PlatformServices.Current.OpenUrl(_updatePage);

    /// <summary>Reads the latest release tag from GitHub once a day; when off, no request is made.</summary>
    public bool CheckForUpdates
    {
        get => _settings.CheckForUpdates;
        set
        {
            if (_settings.CheckForUpdates == value) return;
            _settings.CheckForUpdates = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            if (value) _ = CheckForUpdateAsync();
            else HasUpdate = false;
        }
    }

    /// <summary>In the background: no network, no release or anything unexpected leaves Home as it is.</summary>
    private async Task CheckForUpdateAsync()
    {
        if (!_settings.CheckForUpdates || string.IsNullOrEmpty(VersionNumber)) return;
        try
        {
            using var http = UpdateCheck.CreateHttpClient(VersionNumber);
            var found = await Task.Run(() => UpdateCheck.CheckAsync(http, VersionNumber));
            if (found is null || !_settings.CheckForUpdates) return;
            _updatePage = found.PageUrl;
            UpdateNotice.Set("Home.UpdateAvailable", found.Version);
            HasUpdate = true;
        }
        catch (Exception)
        {
            // A notice is a nicety: never an error.
        }
    }

    public bool HideEmulatorConsole
    {
        get => _settings.HideEmulatorConsole;
        set
        {
            if (_settings.HideEmulatorConsole == value) return;
            _settings.HideEmulatorConsole = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public bool UiSounds
    {
        get => _settings.UiSounds;
        set
        {
            if (_settings.UiSounds == value) return;
            _settings.UiSounds = value;
            Services.UiSounds.Enabled = value;
            if (value) Services.UiSounds.Warm();
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<LanguageOption> Languages => Localizer.Instance.Languages;

    public IReadOnlyList<ChannelOption> Channels { get; } =
        Enum.GetValues<EmulatorChannel>().Select(c => new ChannelOption(c, new LocText($"Channel.{c}"))).ToList();

    public string GameInfo => $"Bloodborne {Bloodborne.TitleId} v{Bloodborne.SupportedAppVersion}";

    public LocText EmulatorStatus { get; } = new("Emulator.Checking");

    [ObservableProperty] public partial LanguageOption SelectedLanguage { get; set; }
    [ObservableProperty] public partial ChannelOption SelectedChannel { get; set; }
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    public partial string? EmulatorPath { get; set; }

    [ObservableProperty] public partial double DownloadProgress { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadEmulatorCommand), nameof(PlayCommand))]
    public partial bool IsBusy { get; set; }

    public async Task InitializeAsync()
    {
        RefreshEmulator();
        // No network here: the patch files come only when Update is pressed on the Patches page. The new version check
        // (a release tag, once a day, unless turned off) runs on its own.
        _ = CheckForUpdateAsync();
        await Game.InitializeAsync();
    }

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        Localizer.Instance.SetLanguage(value.Code);
        _settings.Language = Localizer.Instance.Language;
        SettingsStore.Save(_settings);
    }

    partial void OnSelectedChannelChanged(ChannelOption value)
    {
        if (_settings.EmulatorChannel == value.Value) return; // initial selection from settings
        _settings.EmulatorChannel = value.Value;
        SettingsStore.Save(_settings);
        RefreshEmulator();
    }

    /// <summary>How long the launcher waits for GitHub's release list (a weak connection must not hold the game back).</summary>
    private static readonly TimeSpan ReleaseQueryTimeout = TimeSpan.FromSeconds(10);

    private ShadPs4Downloader.InstalledBuild? _installed;

    /// <summary>The build on disk for the channel; the network is used only when the player asks for a download.</summary>
    private void RefreshEmulator()
    {
        _installed = ShadPs4Downloader.FindInstalled(SelectedChannel.Value);
        EmulatorPath = _installed?.Executable;
        if (_installed is { } local) EmulatorStatus.Set("Emulator.Installed", ShadPs4Release.DisplayNameOf(local.Tag));
        else EmulatorStatus.Set("Emulator.NotInstalled");
        UpdateHome();
    }

    /// <summary>The channel's build on GitHub (one small query); null, with the reason in <see cref="EmulatorStatus"/>, when it cannot be had.</summary>
    private async Task<ShadPs4Release?> ResolveReleaseAsync()
    {
        IsBusy = true;
        EmulatorStatus.Set("Emulator.Querying");
        try
        {
            using var timeout = new CancellationTokenSource(ReleaseQueryTimeout);
            return await _downloader.ResolveAsync(SelectedChannel.Value, timeout.Token);
        }
        catch (Exception ex)
        {
            EmulatorStatus.Set("Emulator.QueryFailed", FriendlyErrors.Describe(ex));
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Play without shadPS4: asks the player and downloads the build only on consent. False when declined or failed.</summary>
    private async Task<bool> DownloadWithConsentAsync()
    {
        var release = await ResolveReleaseAsync();
        if (release is null)
        {
            PlayStatus.Set("Play.DownloadFailed", EmulatorStatus);
            HasPlayError = true;
            return false;
        }
        if (!await AskDownloadAsync(release))
        {
            RefreshEmulator(); // "Not installed" again instead of the release looked up
            return false;
        }
        IsDownloadingEmulator = true;
        try
        {
            if (await InstallEmulatorAsync(release)) return true;
            PlayStatus.Set("Play.DownloadFailed", EmulatorStatus);
            HasPlayError = true;
            return false;
        }
        finally
        {
            IsDownloadingEmulator = false;
            UpdateHome();
        }
    }

    private bool CanDownloadEmulator() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanDownloadEmulator))]
    private async Task DownloadEmulatorAsync()
    {
        if (await ResolveReleaseAsync() is { } release) await InstallEmulatorAsync(release);
    }

    /// <summary>Downloads and unpacks <paramref name="release"/>; false (with the reason in <see cref="EmulatorStatus"/>) on failure.</summary>
    private async Task<bool> InstallEmulatorAsync(ShadPs4Release release)
    {
        IsBusy = true;
        try
        {
            var had = ShadPs4Downloader.TryGetExecutable(ShadPs4Downloader.GetInstallDir(release.Tag)) is not null;
            EmulatorStatus.Set("Emulator.Downloading", release.DisplayName, release.SizeMb, ShadPs4Downloader.RepositoryHost);
            var progress = new Progress<double>(p => DownloadProgress = p * 100);
            var executable = await _downloader.InstallAsync(release, progress);
            _installed = new ShadPs4Downloader.InstalledBuild(release.Tag, executable);
            EmulatorPath = executable;
            EmulatorStatus.Set(had ? "Emulator.UpToDate" : "Emulator.Installed", release.DisplayName);
            return true;
        }
        catch (Exception ex)
        {
            EmulatorStatus.Set("Emulator.InstallFailed", FriendlyErrors.Describe(ex));
            return false;
        }
        finally
        {
            DownloadProgress = 0;
            IsBusy = false;
        }
    }


    [ObservableProperty] public partial bool ShowDownloadOffer { get; set; }

    public LocText DownloadOfferText { get; } = new("DownloadOffer.Text", "", 0, "");

    [ObservableProperty] public partial string? DownloadOfferFile { get; set; }

    private string? _downloadOfferPage;
    private TaskCompletionSource<bool>? _downloadOffer;

    /// <summary>Shows the dialog for <paramref name="release"/>; true on Download, false on Cancel.</summary>
    private Task<bool> AskDownloadAsync(ShadPs4Release release)
    {
        _downloadOffer?.TrySetResult(false);
        DownloadOfferText.Set("DownloadOffer.Text", release.DisplayName, release.SizeMb, ShadPs4Downloader.RepositoryHost);
        DownloadOfferFile = release.AssetName;
        _downloadOfferPage = release.ReleasePage;
        _downloadOffer = new TaskCompletionSource<bool>();
        ShowDownloadOffer = true;
        return _downloadOffer.Task;
    }

    [RelayCommand]
    private void AcceptDownload() => CloseDownloadOffer(true);

    [RelayCommand]
    private void DeclineDownload() => CloseDownloadOffer(false);

    [RelayCommand]
    private void OpenDownloadOfferPage()
    {
        if (_downloadOfferPage is { } page) PlatformServices.Current.OpenUrl(page);
    }

    private void CloseDownloadOffer(bool accepted)
    {
        ShowDownloadOffer = false;
        var offer = _downloadOffer;
        _downloadOffer = null;
        offer?.TrySetResult(accepted);
    }
}
