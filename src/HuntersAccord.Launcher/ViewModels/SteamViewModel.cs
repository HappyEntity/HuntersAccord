using System.Collections.ObjectModel;
using Avalonia.Threading;
using HuntersAccord.Core.Emulator;
using HuntersAccord.Core.Settings;
using HuntersAccord.Core.Steam;
using HuntersAccord.Launcher.Localization;
using HuntersAccord.Launcher.Services;
using HuntersAccord.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HuntersAccord.Launcher.ViewModels;

/// <summary>A FromSoftware game offered for the Steam session. <see cref="Installed"/> is only a hint.</summary>
public sealed record SteamGameOption(FromSoftwareGame Game, bool Installed)
{
    public string Name => Game.Name;
}

public sealed record NetworkOption(CoopNetwork Value, LocText Label);

public enum StatusKind
{
    Ok,
    Warn,
    Error,
    Dev,
}

/// <summary>Settings → Steam and the online line under Play.</summary>
public sealed partial class SteamViewModel : ObservableObject
{
    /// <summary>The game offered first: the newest, owned by the most players.</summary>
    private const uint DefaultAppId = 1245620; // ELDEN RING

    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(5);

    private readonly LauncherSettings _settings;
    private readonly IStorageService _storage;
    private readonly string? _steamRoot = PlatformServices.Current.FindSteamRoot();
    private SteamClientState _client = new(SteamClientStatus.NotRunning, 0, null);
    private SteamApiFile? _library;
    private bool _searching;
    private bool _manualUnfit;

    public SteamViewModel(LauncherSettings settings, IStorageService storage)
    {
        _settings = settings;
        _storage = storage;
        var installed = _steamRoot is null
            ? []
            : SteamLibraryScanner.FindInstalledFromSoftwareGames(_steamRoot).Select(g => g.AppId).ToHashSet();
        foreach (var game in FromSoftwareGames.All.OrderBy(g => g.Priority))
            Games.Add(new SteamGameOption(game, installed.Contains(game.AppId)));
#if HA_PLAYTEST
        // Playtest builds only: Valve's Spacewar test app, for testers without a FromSoftware game on Steam.
        Games.Add(new SteamGameOption(new FromSoftwareGame(480, "Spacewar (playtest)", int.MaxValue), false));
#endif
        SelectedNetwork = Networks.First(n => n.Value == settings.CoopNetwork);
        var appId = settings.SteamAppId == 0 ? DefaultAppId : settings.SteamAppId;
        SelectedGame = Games.FirstOrDefault(g => g.Game.AppId == appId) ?? Games[0];

        CheckClient();
        new DispatcherTimer(CheckEvery, DispatcherPriority.Background, (_, _) => CheckClient()).Start();
        _ = FindLibraryAsync();
        Localizer.Instance.PropertyChanged += (_, _) => Refresh();
    }


    public LocText ClientText { get; } = new("Steam.Client.NotRunning");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClientOk), nameof(ClientWarn), nameof(ClientError))]
    public partial StatusKind ClientKind { get; set; }

    public bool ClientOk => ClientKind == StatusKind.Ok;
    public bool ClientWarn => ClientKind == StatusKind.Warn;
    public bool ClientError => ClientKind == StatusKind.Error;

    public bool ShowRecheck => _client.Status is SteamClientStatus.NotFound or SteamClientStatus.NotRunning or SteamClientStatus.SignedOut;

    public string GameName => _client.GameName ?? "—";

    public bool HasGameName => _client.GameName is not null;

    [RelayCommand]
    private void Recheck() => CheckClient();

    private void CheckClient()
    {
        var state = SteamClientState.Read(_steamRoot, PlatformServices.Current.SteamActiveAccount());
        if (state == _client) return;
        _client = state;
        Refresh();
    }


    public ObservableCollection<SteamGameOption> Games { get; } = [];

    [ObservableProperty] public partial SteamGameOption SelectedGame { get; set; }

    public bool ShowNotOwned => SteamLastStart.Read() is { Owned: false } last && last.AppId == SelectedGame.Game.AppId;

    partial void OnSelectedGameChanged(SteamGameOption value)
    {
        if (_settings.SteamAppId != value.Game.AppId)
        {
            _settings.SteamAppId = value.Game.AppId;
            SettingsStore.Save(_settings);
        }
        Refresh();
    }


    public LocText LibraryText { get; } = new("Steam.Library.Searching");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LibraryOk), nameof(LibraryError))]
    public partial StatusKind LibraryKind { get; set; }

    public bool LibraryOk => LibraryKind == StatusKind.Ok;
    public bool LibraryError => LibraryKind == StatusKind.Error;

    public bool IsManualLibrary => _settings.SteamApiPath is not null;

    [RelayCommand]
    private async Task ChooseLibraryAsync()
    {
        var file = await _storage.PickFileAsync(Localizer.Instance["Steam.ChooseFileTitle"], SteamApiLibrary.FileName,
            SteamApiLibrary.FileName);
        if (file is null) return;
        _settings.SteamApiPath = file;
        SettingsStore.Save(_settings);
        await FindLibraryAsync();
    }

    [RelayCommand]
    private async Task AutomaticLibraryAsync()
    {
        _settings.SteamApiPath = null;
        SettingsStore.Save(_settings);
        await FindLibraryAsync();
    }

    private async Task FindLibraryAsync()
    {
        _searching = true;
        Refresh();
        var manual = _settings.SteamApiPath;
        var root = _steamRoot;
        (SteamApiFile? found, bool unfit) = await Task.Run(() =>
        {
            if (manual is not null)
            {
                var file = SteamApiLibrary.Describe(manual);
                return (file, file is null);
            }
            return (root is null ? null : SteamApiLibrary.FindBest(root), false);
        });
        _library = found;
        _manualUnfit = unfit;
        _searching = false;
        Refresh();
    }


    [ObservableProperty] public partial bool ShowDeveloperTools { get; set; }

    public IReadOnlyList<NetworkOption> Networks { get; } =
    [
        new(CoopNetwork.Steam, new LocText("Steam.Network.Steam")),
        new(CoopNetwork.ThisPc, new LocText("Steam.Network.ThisPc")),
    ];

    [ObservableProperty] public partial NetworkOption SelectedNetwork { get; set; }

    partial void OnSelectedNetworkChanged(NetworkOption value)
    {
        if (_settings.CoopNetwork != value.Value)
        {
            _settings.CoopNetwork = value.Value;
            SettingsStore.Save(_settings);
        }
        Refresh();
    }

    partial void OnShowDeveloperToolsChanged(bool value) => Refresh();

    private bool UsesSteam => !ShowDeveloperTools || SelectedNetwork.Value == CoopNetwork.Steam;


    public LocText OnlineText { get; } = new("Online.SteamNotRunning");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OnlineOk), nameof(OnlineWarn), nameof(OnlineError), nameof(OnlineDev))]
    public partial StatusKind OnlineKind { get; set; }

    public bool OnlineOk => OnlineKind == StatusKind.Ok;
    public bool OnlineWarn => OnlineKind == StatusKind.Warn;
    public bool OnlineError => OnlineKind == StatusKind.Error;
    public bool OnlineDev => OnlineKind == StatusKind.Dev;

    public bool ShowOnlineSettingsLink => OnlineKind == StatusKind.Error;

    /// <summary>
    /// Co-op over Steam when it is the network (null: this PC, as chosen). Without a Steam library too: the game then knows
    /// Steam was asked for and says in game that co-op cannot reach anyone (rather than falling back to this PC silently).
    /// </summary>
    public SteamLaunch? Launch() =>
        UsesSteam ? new SteamLaunch(SelectedGame.Game.AppId, _library?.Path) : null;

    private void Refresh()
    {
        switch (_client.Status)
        {
            case SteamClientStatus.Ready:
                ClientText.Set("Steam.Client.Ready", _client.PersonaName ?? "?");
                ClientKind = StatusKind.Ok;
                break;
            case SteamClientStatus.Offline:
                ClientText.Set("Steam.Client.Offline");
                ClientKind = StatusKind.Warn;
                break;
            case SteamClientStatus.SignedOut:
                ClientText.Set("Steam.Client.SignedOut");
                ClientKind = StatusKind.Warn;
                break;
            case SteamClientStatus.NotRunning:
                ClientText.Set("Steam.Client.NotRunning");
                ClientKind = StatusKind.Warn;
                break;
            default:
                ClientText.Set("Steam.Client.NotFound");
                ClientKind = StatusKind.Error;
                break;
        }

        if (_searching)
        {
            LibraryText.Set("Steam.Library.Searching");
            LibraryKind = StatusKind.Ok;
        }
        else if (_library is { } library)
        {
            if (IsManualLibrary) LibraryText.Set("Steam.Library.Own", library.Path);
            else LibraryText.Set("Steam.Library.Found", library.GameName, library.Version.ToString());
            LibraryKind = StatusKind.Ok;
        }
        else
        {
            LibraryText.Set(_manualUnfit ? "Steam.Library.TooOld" : "Steam.Library.None");
            LibraryKind = StatusKind.Error;
        }

        var notOwned = ShowNotOwned;
        if (!UsesSteam)
        {
            OnlineText.Set("Online.ThisPc");
            OnlineKind = StatusKind.Dev;
        }
        else if (_client.Status == SteamClientStatus.NotFound)
        {
            OnlineText.Set("Online.Unavailable", new LocText("Online.Reason.NoSteam"));
            OnlineKind = StatusKind.Error;
        }
        else if (!_searching && _library is null)
        {
            OnlineText.Set("Online.Unavailable", new LocText("Online.Reason.NoLibrary"));
            OnlineKind = StatusKind.Error;
        }
        else if (notOwned)
        {
            OnlineText.Set("Online.Unavailable", new LocText("Online.Reason.NotOwned", SelectedGame.Name));
            OnlineKind = StatusKind.Error;
        }
        else if (_client.Status is SteamClientStatus.NotRunning or SteamClientStatus.SignedOut)
        {
            OnlineText.Set("Online.SteamNotRunning");
            OnlineKind = StatusKind.Warn;
        }
        else if (_client.Status == SteamClientStatus.Offline)
        {
            OnlineText.Set("Online.SteamOffline");
            OnlineKind = StatusKind.Warn;
        }
        else
        {
            OnlineText.Set("Online.Steam", SelectedGame.Name, _client.GameName ?? "?");
            OnlineKind = StatusKind.Ok;
        }

        OnPropertyChanged(nameof(ShowRecheck));
        OnPropertyChanged(nameof(GameName));
        OnPropertyChanged(nameof(HasGameName));
        OnPropertyChanged(nameof(ShowNotOwned));
        OnPropertyChanged(nameof(IsManualLibrary));
        OnPropertyChanged(nameof(ShowOnlineSettingsLink));
    }
}
