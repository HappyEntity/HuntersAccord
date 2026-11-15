using System.Collections.ObjectModel;
using HuntersAccord.Core.Game;
using HuntersAccord.Core.Pkg;
using HuntersAccord.Core.Settings;
using HuntersAccord.Core.Trophies;
using HuntersAccord.Launcher.Localization;
using HuntersAccord.Launcher.Services;
using HuntersAccord.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HuntersAccord.Launcher.ViewModels;

/// <summary>The colour of the trophy key's status line: plain, fits (success), unchecked (warning), wrong (error).</summary>
public enum TrophyKeyStatusKind
{
    None,
    Ok,
    Warn,
    Error,
}

public sealed record GameInstallOption(GameInstall Install)
{
    public string Path => Install.BasePath;
    public string Version => $"v{Install.EffectiveVersion}";
    public bool IsSupported => Install.IsSupportedVersion;
    public LocText Tag { get; } = Install.IsSupportedVersion ? new LocText("Game.TagSupported") : new LocText("Game.TagNeeds", Bloodborne.SupportedAppVersion);

    public LocText Label { get; } = Install.UpdatePath is null
        ? new LocText("Game.InstallLabel", Install.BasePath, Install.EffectiveVersion)
        : new LocText("Game.InstallLabelUpdate", Install.BasePath, Install.EffectiveVersion);
}

/// <summary>
/// "Game" section: finds Bloodborne as an installed game, an unpacked dump folder or PKG files,
/// and unpacks PKGs when the user has provided keys.
/// </summary>
public sealed partial class GameSectionViewModel(LauncherSettings settings, IStorageService storage) : ObservableObject
{
    private GameScanResult? _scan;
    private PkgKeys? _keys;

    public ObservableCollection<GameInstallOption> Installs { get; } = [];

    public LocText Status { get; } = new("Game.Scanning");
    public LocText BasePkgText { get; } = new("Game.PkgNone");
    public LocText PatchPkgText { get; } = new("Game.PkgNone");
    public LocText KeysText { get; } = new("Game.KeysMissing");
    public LocText InstallText { get; } = new("Game.InstallIdle");

    [ObservableProperty] public partial GameInstallOption? SelectedInstall { get; set; }
    [ObservableProperty] public partial bool IsReady { get; set; }
    [ObservableProperty] public partial bool HasPkg { get; set; }
    [ObservableProperty] public partial string? InstallDestination { get; set; }
    [ObservableProperty] public partial double InstallProgress { get; set; }

    public LocText PanelTitle { get; } = new("Home.NotFoundTitle");
    public LocText PanelText { get; } = new("Home.NotFoundText");
    [ObservableProperty] public partial bool IsScanning { get; set; } = true;
    [ObservableProperty] public partial bool ShowPkgPanel { get; set; }
    [ObservableProperty] public partial string? WrongVersionPath { get; set; }
    [ObservableProperty] public partial string? BasePkgName { get; set; }
    [ObservableProperty] public partial string? BasePkgSize { get; set; }
    [ObservableProperty] public partial string? PatchPkgName { get; set; }
    [ObservableProperty] public partial string? PatchPkgSize { get; set; }
    [ObservableProperty] public partial bool HasKeys { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallFromPkgCommand), nameof(RescanCommand), nameof(BrowseFolderCommand), nameof(ImportKeysCommand), nameof(ChangeDestinationCommand))]
    public partial bool IsInstalling { get; set; }

    private IEnumerable<string> SearchDirectories()
    {
        yield return AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(settings.GameSourceDir)) yield return settings.GameSourceDir;
        if (!string.IsNullOrEmpty(settings.InstallDir)) yield return settings.InstallDir;
        foreach (var dir in GameLocator.ReadShadPs4InstallDirs(PlatformServices.Current.GetShadPs4UserDir()))
            yield return dir;
    }

    public async Task InitializeAsync() => await RescanAsync();

    partial void OnSelectedInstallChanged(GameInstallOption? value)
    {
        UpdateStatus();
        RefreshTrophyKey(); // the key is checked on the copy chosen
        if (value is null || value.Install.BasePath == settings.GamePath) return;
        settings.GamePath = value.Install.BasePath;
        SettingsStore.Save(settings);
    }

    private bool CanRunAction() => !IsInstalling;

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private Task RescanAsync() => ScanAsync(keepInstallText: false);

    private async Task ScanAsync(bool keepInstallText)
    {
        IsScanning = true;
        Status.Set("Game.Scanning");
        var dirs = SearchDirectories().ToList();
        _scan = await Task.Run(() => GameLocator.Scan(dirs));
        _keys = PkgKeys.TryLoad(PkgKeys.DefaultLocations(PlatformServices.Current.GetShadPs4UserDir()));

        Installs.Clear();
        foreach (var install in _scan.Installs.OrderByDescending(i => i.IsSupportedVersion))
            Installs.Add(new GameInstallOption(install));
        SelectedInstall = Installs.FirstOrDefault(i => i.Install.BasePath == settings.GamePath)
                          ?? Installs.FirstOrDefault(i => i.Install == _scan.Best);

        HasPkg = _scan.BasePkg is not null || _scan.PatchPkg is not null;
        SetPkgText(BasePkgText, _scan.BasePkg);
        SetPkgText(PatchPkgText, _scan.PatchPkg);
        InstallDestination = !string.IsNullOrEmpty(settings.InstallDir) ? settings.InstallDir
                           : _scan.BasePkg is { } b ? Path.GetDirectoryName(b.Path)
                           : _scan.PatchPkg is { } p ? Path.GetDirectoryName(p.Path) : null;

        if (_keys is null) KeysText.Set("Game.KeysMissing");
        else KeysText.Set("Game.KeysFound");

        UpdateInstalledState();
        if (!keepInstallText) InstallText.Set(IsPkgInstalled ? "Game.AlreadyUnpacked" : "Game.InstallIdle");

        BasePkgName = _scan.BasePkg is { } bp ? Path.GetFileName(bp.Path) : null;
        BasePkgSize = _scan.BasePkg is { } bs ? $"{Gib(bs.PfsSize)} GB" : null;
        PatchPkgName = _scan.PatchPkg is { } pp ? Path.GetFileName(pp.Path) : null;
        PatchPkgSize = _scan.PatchPkg is { } ps ? $"{Gib(ps.PfsSize)} GB" : null;
        HasKeys = _keys is not null;
        RefreshTrophyKey();
        IsScanning = false;
        UpdateStatus();
        InstallFromPkgCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty] public partial bool IsPkgInstalled { get; set; }

    partial void OnInstallDestinationChanged(string? value)
    {
        UpdateInstalledState();
        InstallFromPkgCommand.NotifyCanExecuteChanged();
    }

    private void UpdateInstalledState()
    {
        var pkgs = new[] { _scan?.BasePkg, _scan?.PatchPkg }.OfType<PkgInfo>().ToList();
        IsPkgInstalled = InstallDestination is { } dest && pkgs.Count > 0 && pkgs.All(p => PkgInstaller.IsInstalled(p, dest));
    }

    private static void SetPkgText(LocText text, PkgInfo? pkg)
    {
        if (pkg is null) text.Set("Game.PkgNone");
        else text.Set("Game.PkgFound", Path.GetFileName(pkg.Path), pkg.AppVersion);
    }

    private void UpdateStatus()
    {
        var install = SelectedInstall?.Install;
        IsReady = install is { IsSupportedVersion: true };

        if (install is null)
            Status.Set(HasPkg ? "Game.StatusPkgOnly" : "Game.StatusNotFound");
        else if (install.IsSupportedVersion)
            Status.Set("Game.StatusReady", install.EffectiveVersion);
        else
            Status.Set("Game.StatusWrongVersion", install.EffectiveVersion, Bloodborne.SupportedAppVersion);

        ShowPkgPanel = !IsReady && HasPkg || IsInstalling;
        WrongVersionPath = null;
        if (ShowPkgPanel)
        {
            PanelTitle.Set("Home.PkgTitle");
            PanelText.Set("Home.PkgText");
        }
        else if (install is { IsSupportedVersion: false })
        {
            PanelTitle.Set("Home.WrongVersionTitle");
            PanelText.Set("Home.WrongVersionText", install.EffectiveVersion, Bloodborne.SupportedAppVersion);
            WrongVersionPath = install.BasePath;
        }
        else
        {
            PanelTitle.Set("Home.NotFoundTitle");
            PanelText.Set("Home.NotFoundText", Bloodborne.TitleId);
        }
    }

    partial void OnIsInstallingChanged(bool value) => UpdateStatus();

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task BrowseFolderAsync()
    {
        var folder = await storage.PickFolderAsync(Localizer.Instance["Game.BrowseTitle"]);
        if (folder is null) return;

        // The picked folder may itself be the game folder; scan its parent too so siblings (-UPDATE) are found.
        settings.GameSourceDir = GameLocator.TryReadInstall(folder) is not null
            ? Path.GetDirectoryName(folder) ?? folder
            : folder;
        SettingsStore.Save(settings);
        await RescanAsync();
    }

    /// <summary>
    /// Merges the chosen keys.json into the launcher's own (<see cref="KeyFile.Import"/>): its PKG keys when complete, its
    /// trophy key when it opens this game copy's trophies (or, with no copy to check against, when none is stored yet).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task ImportKeysAsync()
    {
        var file = await storage.PickFileAsync(Localizer.Instance["Game.KeysImportTitle"], "keys.json", "*.json");
        if (file is null) return;
        try
        {
            var check = TrophyKeyChecker();
            var result = KeyFile.Import(file, KeyFile.OwnFile, key => check(key));
            _keys = PkgKeys.TryLoad(PkgKeys.DefaultLocations(PlatformServices.Current.GetShadPs4UserDir()));
            HasKeys = _keys is not null;
            KeysText.Set(HasKeys ? "Game.KeysFound" : "Game.KeysMissing");
            RefreshTrophyKey();
            switch (result.TrophyKey)
            {
                case ImportedTrophyKey.Wrong: SetTrophyKeyStatus("Game.TrophyKeyImportWrong", TrophyKeyStatusKind.Error); break;
                case ImportedTrophyKey.KeptStored: SetTrophyKeyStatus("Game.TrophyKeyKept", TrophyKeyStatusKind.Warn); break;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or FormatException)
        {
            KeysText.Set("Game.KeysInvalid", e.Message);
            SetTrophyKeyStatus("Game.KeysInvalid", TrophyKeyStatusKind.Error, e.Message);
        }
        InstallFromPkgCommand.NotifyCanExecuteChanged();
    }

    // ---- The trophy key (Settings → Game → Trophies) ---------------------------------------------------------------

    /// <summary>The key field: filled from keys.json (the launcher's own, then shadPS4's), or typed in. Never logged.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveTrophyKeyCommand))]
    public partial string TrophyKeyText { get; set; } = "";

    /// <summary>The key shown in the clear (masked by default).</summary>
    [ObservableProperty] public partial bool ShowTrophyKey { get; set; }

    partial void OnShowTrophyKeyChanged(bool value) => TrophyKeyReveal.Set(value ? "Game.TrophyKeyHide" : "Game.TrophyKeyShow");

    /// <summary>The show / hide button's words.</summary>
    public LocText TrophyKeyReveal { get; } = new("Game.TrophyKeyShow");

    public LocText TrophyKeyStatus { get; } = new("Game.TrophyKeyNone");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrophyKeyOk), nameof(TrophyKeyError), nameof(TrophyKeyWarn), nameof(TrophyKeyPlain))]
    public partial TrophyKeyStatusKind TrophyKeyStatusKind { get; set; }

    public bool TrophyKeyOk => TrophyKeyStatusKind == TrophyKeyStatusKind.Ok;
    public bool TrophyKeyError => TrophyKeyStatusKind == TrophyKeyStatusKind.Error;
    public bool TrophyKeyWarn => TrophyKeyStatusKind == TrophyKeyStatusKind.Warn;
    public bool TrophyKeyPlain => TrophyKeyStatusKind == TrophyKeyStatusKind.None;

    /// <summary>A key that opens the selected copy's trophies (null: none): the game's own trophy texts come from it.</summary>
    [ObservableProperty] public partial byte[]? ValidTrophyKey { get; set; }

    private string _trophyKeyLoaded = "";

    private void SetTrophyKeyStatus(string key, TrophyKeyStatusKind kind, params object?[] args)
    {
        TrophyKeyStatus.Set(key, args);
        TrophyKeyStatusKind = kind;
    }

    /// <summary>How a key is tried on the selected copy (its trophy pack and NP communication id), read once per call.</summary>
    private Func<byte[], TrophyKeyCheck> TrophyKeyChecker()
    {
        var game = SelectedInstall?.Install;
        var pack = game is null ? null : TrophyPack.Find(game.BasePath, game.UpdatePath) is { } path ? TrophyPack.Open(path) : null;
        var commId = game is null ? null : TrophyKey.CommIdOf(game.BasePath, game.UpdatePath, TrophyTextSource.TitleIdOf(game));
        return key => TrophyKey.Check(key, pack, commId);
    }

    /// <summary>The key in use from the key files and its status line (after a scan, a copy chosen, an import).</summary>
    public void RefreshTrophyKey()
    {
        var choice = TrophyKey.Choose(
            [KeyFile.ReadTrophyKey(KeyFile.OwnFile), KeyFile.ReadTrophyKey(Path.Combine(PlatformServices.Current.GetShadPs4UserDir(), PkgKeys.FileName))],
            TrophyKeyChecker());
        _trophyKeyLoaded = choice.Text ?? "";
        TrophyKeyText = _trophyKeyLoaded;
        ValidTrophyKey = choice.State == TrophyKeyState.Valid ? choice.Key : null;
        switch (choice.State)
        {
            case TrophyKeyState.Valid: SetTrophyKeyStatus("Game.TrophyKeyValid", TrophyKeyStatusKind.Ok); break;
            case TrophyKeyState.Unverified: SetTrophyKeyStatus("Game.TrophyKeyNoGame", TrophyKeyStatusKind.Warn); break;
            case TrophyKeyState.Wrong: SetTrophyKeyStatus("Game.TrophyKeyFileWrong", TrophyKeyStatusKind.Error); break;
            default: SetTrophyKeyStatus("Game.TrophyKeyNone", TrophyKeyStatusKind.None); break;
        }
    }

    private bool CanSaveTrophyKey() => !string.IsNullOrWhiteSpace(TrophyKeyText) && TrophyKeyText.Trim() != _trophyKeyLoaded.Trim();

    /// <summary>
    /// The typed key: saved into the launcher's own keys.json only when it opens this copy's trophies (or, with no copy to
    /// check against, when none is stored yet); a wrong one is an error at the field and is not saved.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveTrophyKey))]
    private void SaveTrophyKey()
    {
        if (TrophyKey.Parse(TrophyKeyText) is not { } key)
        {
            SetTrophyKeyStatus("Game.TrophyKeyBadFormat", TrophyKeyStatusKind.Error);
            return;
        }
        ImportedTrophyKey outcome;
        try
        {
            outcome = KeyFile.StoreTrophyKey(KeyFile.OwnFile, key, TrophyKeyChecker()(key));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            SetTrophyKeyStatus("Game.TrophyKeySaveFailed", TrophyKeyStatusKind.Error, FriendlyErrors.Describe(e));
            return;
        }
        switch (outcome)
        {
            case ImportedTrophyKey.Valid:
                RefreshTrophyKey();
                SetTrophyKeyStatus("Game.TrophyKeySaved", TrophyKeyStatusKind.Ok);
                break;
            case ImportedTrophyKey.Unverified:
                RefreshTrophyKey();
                SetTrophyKeyStatus("Game.TrophyKeySavedNoGame", TrophyKeyStatusKind.Warn);
                break;
            case ImportedTrophyKey.KeptStored:
                SetTrophyKeyStatus("Game.TrophyKeyKept", TrophyKeyStatusKind.Warn);
                break;
            default:
                SetTrophyKeyStatus("Game.TrophyKeyWrong", TrophyKeyStatusKind.Error);
                break;
        }
        SaveTrophyKeyCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ToggleTrophyKey() => ShowTrophyKey = !ShowTrophyKey;

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task ChangeDestinationAsync()
    {
        var folder = await storage.PickFolderAsync(Localizer.Instance["Game.DestinationTitle"]);
        if (folder is null) return;
        settings.InstallDir = folder;
        SettingsStore.Save(settings);
        InstallDestination = folder;
    }

    private CancellationTokenSource? _installCts;

    private bool CanInstallFromPkg() => !IsInstalling && !IsPkgInstalled && _scan?.BasePkg is not null && _keys is not null;

    [RelayCommand(CanExecute = nameof(CanInstallFromPkg))]
    private async Task InstallFromPkgAsync()
    {
        if (_scan?.BasePkg is not { } basePkg || _keys is null || InstallDestination is null) return;

        IsInstalling = true;
        _installCts = new CancellationTokenSource();
        CancelInstallCommand.NotifyCanExecuteChanged();
        try
        {
            if (!await Task.Run(() => _keys.CanDecrypt(basePkg.Path)))
            {
                InstallText.Set("Game.KeysWrong");
                return;
            }

            var destination = InstallDestination;
            var pkgs = new[] { basePkg, _scan.PatchPkg }.OfType<PkgInfo>().ToList();
            for (var i = 0; i < pkgs.Count; i++)
            {
                var pkg = pkgs[i];
                if (PkgInstaller.IsInstalled(pkg, destination)) continue;

                var step = $"{i + 1}/{pkgs.Count}";
                var label = pkg.IsPatch ? new LocText("Game.PartPatch", pkg.AppVersion) : new LocText("Game.PartBase");
                var speed = new TransferRate();
                var active = true; // Progress<T> posts asynchronously: ignore reports that arrive after this part finished.
                InstallText.Set("Game.Preparing", step, label);
                var progress = new Progress<PkgInstallProgress>(p =>
                {
                    if (!active || p.BytesDone >= p.BytesTotal) return;
                    InstallProgress = p.Fraction * 100;
                    var (rate, eta) = speed.Update(p.BytesDone, p.BytesTotal);
                    InstallText.Set("Game.Installing", step, label, p.Fraction * 100,
                        Gib(p.BytesDone), Gib(p.BytesTotal), rate / 1048576, FormatEta(eta), p.FilesDone, p.FilesTotal);
                });
                try
                {
                    await PkgInstaller.InstallAsync(pkg.Path, destination, _keys, progress, _installCts.Token);
                }
                finally
                {
                    active = false;
                }
            }

            InstallText.Set("Game.InstallDone");
            settings.GamePath = Path.Combine(destination, basePkg.InstallFolderName);
            SettingsStore.Save(settings);
        }
        catch (OperationCanceledException)
        {
            InstallText.Set("Game.InstallCancelled");
        }
        catch (NotEnoughSpaceException e)
        {
            InstallText.Set("Game.NoSpace", Gib(e.Required), Gib(e.Available));
        }
        catch (Exception e)
        {
            InstallText.Set("Game.InstallFailed", FriendlyErrors.Describe(e));
        }
        finally
        {
            InstallProgress = 0;
            IsInstalling = false;
            _installCts.Dispose();
            _installCts = null;
            CancelInstallCommand.NotifyCanExecuteChanged();
        }
        await ScanAsync(keepInstallText: true);
    }

    private bool CanCancelInstall() => _installCts is not null;

    [RelayCommand(CanExecute = nameof(CanCancelInstall))]
    private void CancelInstall() => _installCts?.Cancel();

    private static string Gib(long bytes) => (bytes / 1073741824.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatEta(TimeSpan? eta) => eta is not { } t ? "…"
        : t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
        : $"{t.Minutes}:{t.Seconds:00}";

    private sealed class TransferRate
    {
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        private double _rate;
        private long _lastBytes;
        private TimeSpan _lastTime;

        public (double BytesPerSecond, TimeSpan? Eta) Update(long done, long total)
        {
            var now = _clock.Elapsed;
            var dt = (now - _lastTime).TotalSeconds;
            if (dt >= 0.5)
            {
                var instant = (done - _lastBytes) / dt;
                _rate = _rate == 0 ? instant : _rate * 0.8 + instant * 0.2;
                _lastBytes = done;
                _lastTime = now;
            }
            TimeSpan? eta = _rate > 0 && now.TotalSeconds > 2 ? TimeSpan.FromSeconds((total - done) / _rate) : null;
            return (_rate, eta);
        }
    }
}
