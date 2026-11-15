using HuntersAccord.Core.Emulator;
using HuntersAccord.Core.Game;
using HuntersAccord.Core.Input;
using HuntersAccord.Core.Settings;
using HuntersAccord.Launcher.Localization;
using HuntersAccord.Launcher.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HuntersAccord.Launcher.ViewModels;

public abstract record DisplayChoice(LocText Label);

public sealed record ScreenModeChoice(ScreenMode Value, LocText Label) : DisplayChoice(Label);

public sealed record VSyncChoice(VSyncMode Value, LocText Label) : DisplayChoice(Label);

public sealed record WindowSizeChoice((int W, int H) Value, LocText Label) : DisplayChoice(Label);

public sealed record ResolutionChoice(ScreenResolution Value, LocText Label) : DisplayChoice(Label);

public sealed record KeyToken(string Text, bool IsKey, bool IsOff);

/// <summary>One row of the shadPS4 keys table; a click turns the hotkey off or on, and a key the mod's controls use keeps it off.</summary>
public sealed record HotkeyRow(IReadOnlyList<string> Names, IReadOnlyList<KeyToken> Keys, LocText Action, bool IsTaken, bool IsTurnedOff)
{
    public bool IsOff => IsTaken || IsTurnedOff;

    public bool CanToggle => !IsTaken;

    public bool ShowTurnedOff => IsTurnedOff && !IsTaken;
}

/// <summary>Settings → Display: shadPS4's picture settings, shader cache and hotkeys.</summary>
public sealed partial class DisplayViewModel : ObservableObject
{
    private readonly LauncherSettings _settings;
    private DisplaySettings Display => _settings.Display;

    public DisplayViewModel(LauncherSettings settings)
    {
        _settings = settings;
        SelectedScreenMode = ScreenModes.First(m => m.Value == Display.ScreenMode);
        SelectedVSync = VSyncModes.First(m => m.Value == Display.VSync);
        Resolutions = BuildResolutions();
        SelectedResolution = Resolutions.First(r => r.Value == Display.Resolution);
        WindowSizes = BuildWindowSizes(_screenWidth, _screenHeight);
        SelectedWindowSize = PickWindowSize();
        RefreshResolutionHints();
        RefreshHotkeys();
        _ = RefreshCacheAsync();
    }


    public IReadOnlyList<ScreenModeChoice> ScreenModes { get; } =
        Enum.GetValues<ScreenMode>().Select(m => new ScreenModeChoice(m, new LocText($"Display.Mode.{m}"))).ToList();

    [ObservableProperty] public partial ScreenModeChoice SelectedScreenMode { get; set; }

    partial void OnSelectedScreenModeChanged(ScreenModeChoice value)
    {
        OnPropertyChanged(nameof(IsWindowed));
        if (Display.ScreenMode == value.Value) return;
        Display.ScreenMode = value.Value;
        Save();
        RefreshResolutionHints();
    }

    public bool IsWindowed => SelectedScreenMode.Value == ScreenMode.Windowed;


    private int _screenWidth = int.MaxValue, _screenHeight = int.MaxValue;

    public IReadOnlyList<ResolutionChoice> Resolutions { get; }

    [ObservableProperty] public partial ResolutionChoice SelectedResolution { get; set; }

    partial void OnSelectedResolutionChanged(ResolutionChoice value)
    {
        if (Display.Resolution == value.Value) return;
        Display.Resolution = value.Value;
        // The window takes the new shape (about as tall as it was): the frame fills it, no bars.
        if (!value.Value.Fills(Display.WindowWidth, Display.WindowHeight))
            (Display.WindowWidth, Display.WindowHeight) = value.Value.WindowNear(Display.WindowHeight, _screenWidth, _screenHeight);
        Save();
        WindowSizes = BuildWindowSizes(_screenWidth, _screenHeight);
        SelectedWindowSize = PickWindowSize();
        RefreshResolutionHints();
    }

    public LocText ResolutionMemory { get; } = new("Display.Empty");

    [ObservableProperty] public partial bool HasResolutionMemory { get; set; }

    public LocText ResolutionBars { get; } = new("Display.Empty");

    [ObservableProperty] public partial bool HasResolutionBars { get; set; }

    private void RefreshResolutionHints()
    {
        var r = Display.Resolution;
        HasResolutionMemory = r.ExtraGraphicsMemoryMiB > 0;
        if (HasResolutionMemory) ResolutionMemory.Set("Display.ResolutionMemory", r.ExtraGraphicsMemoryMiB);
        var known = _screenWidth != int.MaxValue;
        var screen = known ? new ScreenResolution(_screenWidth, _screenHeight) : r;
        HasResolutionBars = !IsWindowed && known && !r.Fills(screen.Width, screen.Height);
        if (HasResolutionBars)
            ResolutionBars.Set(r.Aspect > screen.Aspect ? "Display.ResolutionBarsTopBottom" : "Display.ResolutionBarsSides",
                AspectText(screen));
    }

    private IReadOnlyList<ResolutionChoice> BuildResolutions()
    {
        var sizes = ScreenResolution.Presets.ToList();
        if (!sizes.Contains(Display.Resolution)) sizes.Add(Display.Resolution); // a size set by hand stays in the list
        // By shape (16:9 first, as the game's own), then by size.
        return [.. sizes.OrderBy(s => s.Kind == AspectKind.Standard ? 0 : s.Kind == AspectKind.Tall ? 1 : s.Kind == AspectKind.Wide ? 2 :
                s.Kind == AspectKind.SuperWide ? 3 : 4).ThenBy(s => s.Pixels)
            .Select(s => new ResolutionChoice(s, new LocText(s.IsStock ? "Display.ResolutionGame" : "Display.ResolutionValue",
                s.Width, s.Height, AspectText(s))))];
    }

    private static string AspectText(ScreenResolution r) => r.Kind switch
    {
        AspectKind.Standard => "16:9",
        AspectKind.Tall => "16:10",
        AspectKind.Wide => "21:9",
        AspectKind.SuperWide => "32:9",
        _ => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{r.Aspect:0.##}:1"),
    };

    [ObservableProperty] public partial IReadOnlyList<WindowSizeChoice> WindowSizes { get; private set; }

    [ObservableProperty] public partial WindowSizeChoice? SelectedWindowSize { get; set; }

    partial void OnSelectedWindowSizeChanged(WindowSizeChoice? value)
    {
        if (value is null || (Display.WindowWidth, Display.WindowHeight) == value.Value) return;
        (Display.WindowWidth, Display.WindowHeight) = value.Value;
        Save();
    }

    public void LimitToScreen(int width, int height)
    {
        (_screenWidth, _screenHeight) = (width, height);
        WindowSizes = BuildWindowSizes(width, height);
        SelectedWindowSize = PickWindowSize();
        RefreshResolutionHints();
    }

    /// <summary>Window sizes of the resolution's shape up to the monitor's; a stored size of another shape gives way to the nearest of this one.</summary>
    private IReadOnlyList<WindowSizeChoice> BuildWindowSizes(int maxWidth, int maxHeight)
    {
        var resolution = Display.Resolution;
        var sizes = resolution.WindowSizes(maxWidth, maxHeight).Select(s => (s.Width, s.Height)).ToList();
        var current = (Display.WindowWidth, Display.WindowHeight);
        if (!resolution.Fills(current.WindowWidth, current.WindowHeight))
        {
            current = resolution.WindowNear(current.WindowHeight, maxWidth, maxHeight);
            (Display.WindowWidth, Display.WindowHeight) = current;
            Save();
        }
        if (!sizes.Contains(current)) sizes.Add(current);
        return [.. sizes.OrderBy(s => s.Item1 * (long)s.Item2).Select(s => new WindowSizeChoice(s, new LocText("Display.WindowSizeValue", s.Item1, s.Item2)))];
    }

    private WindowSizeChoice PickWindowSize() =>
        WindowSizes.First(s => s.Value == (Display.WindowWidth, Display.WindowHeight));

    public IReadOnlyList<VSyncChoice> VSyncModes { get; } =
        Enum.GetValues<VSyncMode>().Select(m => new VSyncChoice(m, new LocText($"Display.VSync.{m}"))).ToList();

    [ObservableProperty] public partial VSyncChoice SelectedVSync { get; set; }

    partial void OnSelectedVSyncChanged(VSyncChoice value)
    {
        if (Display.VSync == value.Value) return;
        Display.VSync = value.Value;
        Save();
    }

    public bool Fsr
    {
        get => Display.Fsr;
        set => Set(Display.Fsr, value, v => Display.Fsr = v);
    }

    public double Sharpness
    {
        get => Display.SharpnessPercent;
        set
        {
            var percent = (int)Math.Round(value);
            if (Display.SharpnessPercent == percent) return;
            Display.SharpnessPercent = percent;
            Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SharpnessText));
        }
    }

    public string SharpnessText => $"{Display.SharpnessPercent} %";

    public bool ShowFps
    {
        get => Display.ShowFps;
        set => Set(Display.ShowFps, value, v => Display.ShowFps = v);
    }


    public bool ShaderCache
    {
        get => Display.ShaderCache;
        set => Set(Display.ShaderCache, value, v => Display.ShaderCache = v);
    }

    public LocText CacheStatus { get; } = new("Display.CacheEmpty");

    [ObservableProperty] public partial bool CacheCleared { get; set; }
    [ObservableProperty] public partial bool CacheError { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearCacheCommand))]
    public partial bool HasCache { get; set; }

    /// <summary>Clear was pressed once: the second press clears (it disarms by itself after a few seconds).</summary>
    [ObservableProperty] public partial bool ClearArmed { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearCacheCommand))]
    [NotifyPropertyChangedFor(nameof(IsGameStopped))]
    public partial bool IsGameRunning { get; set; }

    public bool IsGameStopped => !IsGameRunning;

    partial void OnIsGameRunningChanged(bool value)
    {
        ClearArmed = false;
        if (!value) _ = RefreshCacheAsync(); // the game has just built more shaders
    }

    private static IEnumerable<EmulatorProfile> Profiles =>
        new[] { EmulatorProfile.Default, EmulatorProfile.ForPlayer(2) }.Where(p => p.Exists);

    public async Task RefreshCacheAsync()
    {
        var bytes = await Task.Run(() => Profiles.Sum(p => p.ShaderCacheSize(Bloodborne.TitleId)));
        HasCache = bytes > 0;
        CacheCleared = CacheError = false;
        if (bytes > 0) CacheStatus.Set("Display.CacheSize", Math.Max(1, (long)Math.Round(bytes / 1048576.0)));
        else CacheStatus.Set("Display.CacheEmpty");
    }

    private bool CanClearCache() => HasCache && !IsGameRunning;

    private CancellationTokenSource? _disarm;

    // Concurrent: the first press waits to disarm, and the second must not find the command busy (disabled).
    [RelayCommand(CanExecute = nameof(CanClearCache), AllowConcurrentExecutions = true)]
    private async Task ClearCacheAsync()
    {
        _disarm?.Cancel();
        if (!ClearArmed)
        {
            ClearArmed = true;
            var disarm = _disarm = new CancellationTokenSource();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(4), disarm.Token);
                ClearArmed = false;
            }
            catch (TaskCanceledException)
            {
            }
            return;
        }
        ClearArmed = false;
        try
        {
            await Task.Run(() =>
            {
                foreach (var profile in Profiles) profile.ClearShaderCache(Bloodborne.TitleId);
            });
            HasCache = false;
            CacheStatus.Set("Display.CacheCleared");
            CacheCleared = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            CacheStatus.Set("Display.CacheClearFailed", FriendlyErrors.Describe(e));
            CacheError = true;
        }
    }


    [ObservableProperty] public partial IReadOnlyList<HotkeyRow> Hotkeys { get; private set; } = [];

    public void RefreshHotkeys()
    {
        var modKeys = ShadPs4Hotkeys.ModKeys(GameSettings.Load());
        var byName = ShadPs4Hotkeys.Kept.ToDictionary(h => h.Name);
        HotkeyRow Row(string action, params string[] names)
        {
            var hotkeys = names.Select(n => byName[n]).ToList();
            var taken = hotkeys.All(h => ShadPs4Hotkeys.IsTaken(h, modKeys));
            var turnedOff = names.All(Display.HotkeysOff.Contains);
            var off = taken || turnedOff;
            // "Ctrl + Shift + End"; several hotkeys on one line (volume) as "Num + / Num −".
            var tokens = new List<KeyToken>();
            foreach (var hotkey in hotkeys)
            {
                if (tokens.Count > 0) tokens.Add(new KeyToken("/", IsKey: false, off));
                foreach (var key in hotkey.Keys.Split('+'))
                {
                    if (tokens.Count > 0 && tokens[^1].IsKey) tokens.Add(new KeyToken("+", IsKey: false, off));
                    tokens.Add(new KeyToken(KeyLabel(key), IsKey: true, off));
                }
            }
            return new HotkeyRow(names, tokens, new LocText($"Display.Key.{action}"), taken, turnedOff);
        }
        Hotkeys =
        [
            Row("Fullscreen", "hotkey_fullscreen"),
            Row("Fps", "hotkey_show_fps"),
            Row("Pause", "hotkey_pause"),
            Row("Screenshot", "hotkey_screenshot_with_overlays"),
            Row("Quit", "hotkey_quit"),
            Row("Volume", "hotkey_volume_up", "hotkey_volume_down"),
        ];
    }

    [RelayCommand]
    private void ToggleHotkey(HotkeyRow row)
    {
        if (row.IsTaken) return;
        foreach (var name in row.Names)
        {
            Display.HotkeysOff.Remove(name);
            if (!row.IsTurnedOff) Display.HotkeysOff.Add(name);
        }
        Save();
        RefreshHotkeys();
    }

    private static string KeyLabel(string key) => key switch
    {
        "LAlt" or "RAlt" => "Alt",
        "LCtrl" or "RCtrl" => "Ctrl",
        "LShift" or "RShift" => "Shift",
        "KpPlus" => "Num +",
        "KpMinus" => "Num −",
        _ => key,
    };


    private void Set(bool current, bool value, Action<bool> apply, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (current == value) return;
        apply(value);
        Save();
        OnPropertyChanged(name);
    }

    private void Save() => SettingsStore.Save(_settings);
}
