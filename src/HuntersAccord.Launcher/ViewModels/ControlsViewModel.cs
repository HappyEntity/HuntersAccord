using Avalonia.Media;
using HuntersAccord.Core.Emulator;
using HuntersAccord.Core.Game;
using HuntersAccord.Core.Input;
using HuntersAccord.Core.Settings;
using HuntersAccord.Launcher.Localization;
using HuntersAccord.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HuntersAccord.Launcher.ViewModels;

/// <summary>A PS4 button as drawn in the launcher: text in a keycap or an outlined shape, on a 12x12 grid.</summary>
public sealed record PadGlyph(string? Text, Geometry? Outline, Geometry? Fill)
{
    public bool IsText => Text is not null;

    /// <summary>The D-pad's arms are 3 units wide: a thinner outline leaves the pressed one visibly filled.</summary>
    public double Stroke => Fill is null ? 1.4 : 1.0;

    private static readonly Dictionary<string, PadGlyph> Glyphs = new()
    {
        ["cross"] = Shape("M2,2 L10,10 M10,2 L2,10"),
        ["circle"] = Shape("M1.6,6 A4.4,4.4 0 1 1 10.4,6 A4.4,4.4 0 1 1 1.6,6 Z"),
        ["square"] = Shape("M2,2 H10 V10 H2 Z"),
        ["triangle"] = Shape("M6,1.6 L10.6,9.8 L1.4,9.8 Z"),
        ["pad_up"] = DPad("M4.5,1 H7.5 V4.5 H4.5 Z"),
        ["pad_down"] = DPad("M4.5,7.5 H7.5 V11 H4.5 Z"),
        ["pad_left"] = DPad("M1,4.5 H4.5 V7.5 H1 Z"),
        ["pad_right"] = DPad("M7.5,4.5 H11 V7.5 H7.5 Z"),
        ["options"] = new("OPT", null, null),
        ["back"] = new("TP", null, null),
        ["touchpad_center"] = new("TP", null, null),
    };

    private const string DPadOutline = "M4.5,1 H7.5 V4.5 H11 V7.5 H7.5 V11 H4.5 V7.5 H1 V4.5 H4.5 Z";

    private static PadGlyph Shape(string path) => new(null, Geometry.Parse(path), null);

    private static PadGlyph DPad(string arm) => new(null, Geometry.Parse(DPadOutline), Geometry.Parse(arm));

    public static PadGlyph For(string name) =>
        Glyphs.TryGetValue(name, out var glyph) ? glyph : new(name.ToUpperInvariant(), null, null);
}

public sealed record PadInputChoice(string Input, PadGlyph? Glyph, LocText Name, string Xbox)
{
    public bool IsUnassigned => Input == GamepadSettings.Unassigned;
}

public sealed partial class PadButtonRow(ControlsViewModel owner, string output, LocText action) : ObservableObject
{
    public string Output { get; } = output;
    public PadGlyph Glyph { get; } = PadGlyph.For(output);
    public LocText Action { get; } = action;
    public IReadOnlyList<PadInputChoice> Choices => owner.Choices;

    [ObservableProperty] public partial PadInputChoice? Selected { get; set; }

    partial void OnSelectedChanged(PadInputChoice? value)
    {
        if (value is not null) owner.Assign(this, value.Input);
    }

    [ObservableProperty] public partial bool IsModified { get; set; }

    [ObservableProperty] public partial bool IsUnassigned { get; set; }

    public LocText Also { get; } = new("Controls.Also", "");

    [ObservableProperty] public partial bool HasAlso { get; set; }
}

public sealed record PadButtonGroup(LocText Title, IReadOnlyList<PadButtonRow> Rows);

public sealed record StickChoice(bool IsRight, LocText Label);

public sealed partial class LightSwatch(string key, string color) : ObservableObject
{
    public string Color { get; } = color;
    public IBrush Brush { get; } = new SolidColorBrush(Avalonia.Media.Color.Parse(color));
    public LocText Name { get; } = new($"Controls.Light.{key}");
    public bool IsOff => Color == "#000000";
    public bool IsColor => !IsOff;

    [ObservableProperty] public partial bool IsSelected { get; set; }
}

/// <summary>Settings → Controls: the gamepad as shadPS4 hands it to the game, written to its input config at every start.</summary>
public sealed partial class ControlsViewModel : ObservableObject
{
    private readonly LauncherSettings _settings;
    private GamepadSettings Pad => _settings.Gamepad;
    private bool _loading;

    public ControlsViewModel(LauncherSettings settings)
    {
        _settings = settings;
        Choices =
        [
            .. PadButtons.Inputs.Select(i => new PadInputChoice(i.Input, PadGlyph.For(i.Input), new LocText($"Controls.Pad.{i.Input}"), i.Xbox)),
            new PadInputChoice(GamepadSettings.Unassigned, null, new LocText("Controls.Unassigned"), ""),
        ];
        var rows = PadButtons.Outputs.Select(o => new PadButtonRow(this, o.Output,
            new LocText(o.Output == "touchpad_center" ? "Controls.Touchpad" : $"GameText.Key.{o.Action}"))).ToList();
        _rows = rows;
        Groups =
        [
            new(new LocText("GameText.KeysPage.Combat"), rows[0..4]),
            new(new LocText("GameText.KeysPage.Movement"), rows[4..7]),
            new(new LocText("GameText.KeysPage.Items"), rows[7..14]),
            new(new LocText("GameText.KeysPage.Menus"), rows[14..]),
        ];
        SelectedMove = Sticks[Pad.SwapSticks ? 1 : 0];
        SelectedCamera = Sticks[Pad.SwapSticks ? 0 : 1];
        (_red, _green, _blue) = PadButtons.ParseColor(Pad.LightColor);
        Refresh();
        SetColor();
    }


    private readonly List<PadButtonRow> _rows;

    public IReadOnlyList<PadInputChoice> Choices { get; }

    public IReadOnlyList<PadButtonGroup> Groups { get; }

    internal void Assign(PadButtonRow row, string input)
    {
        if (_loading || PadButtons.InputFor(Pad, row.Output) == input) return;
        PadButtons.Assign(Pad, row.Output, input);
        Save();
        Refresh();
    }

    private void Refresh()
    {
        _loading = true;
        var shared = PadButtons.SharedWith(Pad);
        foreach (var row in _rows)
        {
            var input = PadButtons.InputFor(Pad, row.Output);
            row.Selected = Choices.First(c => c.Input == input);
            row.IsModified = input != PadButtons.DefaultInput(row.Output);
            row.IsUnassigned = input == GamepadSettings.Unassigned;
            row.HasAlso = shared.TryGetValue(row.Output, out var others);
            // Its first partner is enough to find the clash (the partner's own row names this one).
            if (others is not null) row.Also.Set("Controls.Also", _rows.First(r => r.Output == others[0]).Action);
        }
        _loading = false;
        ResetButtonsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SwapCrossCircle()
    {
        PadButtons.SwapCrossCircle(Pad);
        Save();
        Refresh();
    }

    [ObservableProperty] public partial bool ResetArmed { get; set; }

    private CancellationTokenSource? _disarm;

    private bool CanResetButtons() => Pad.Buttons.Count > 0;

    // Concurrent: the first press waits to disarm, and the second must not find the command busy (disabled).
    [RelayCommand(CanExecute = nameof(CanResetButtons), AllowConcurrentExecutions = true)]
    private async Task ResetButtonsAsync()
    {
        _disarm?.Cancel();
        if (!ResetArmed)
        {
            ResetArmed = true;
            var disarm = _disarm = new CancellationTokenSource();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(4), disarm.Token);
                ResetArmed = false;
            }
            catch (TaskCanceledException)
            {
            }
            return;
        }
        ResetArmed = false;
        Pad.Buttons.Clear();
        Save();
        Refresh();
    }


    public IReadOnlyList<StickChoice> Sticks { get; } =
        [new(false, new LocText("Controls.LeftStick")), new(true, new LocText("Controls.RightStick"))];

    [ObservableProperty] public partial StickChoice SelectedMove { get; set; }

    [ObservableProperty] public partial StickChoice SelectedCamera { get; set; }

    // The two lists are a pair: one stick cannot do both, so choosing one moves the other.
    partial void OnSelectedMoveChanged(StickChoice value) => SetSwap(value.IsRight);

    partial void OnSelectedCameraChanged(StickChoice value) => SetSwap(!value.IsRight);

    private void SetSwap(bool swap)
    {
        if (SelectedMove is null || SelectedCamera is null) return; // the constructor sets the first of them
        SelectedMove = Sticks[swap ? 1 : 0];
        SelectedCamera = Sticks[swap ? 0 : 1];
        if (Pad.SwapSticks == swap) return;
        Pad.SwapSticks = swap;
        Save();
    }

    public double LeftDeadZone
    {
        get => Pad.LeftDeadZonePercent;
        set => SetDeadZone(value, Pad.LeftDeadZonePercent, v => Pad.LeftDeadZonePercent = v, nameof(LeftDeadZoneText));
    }

    public double RightDeadZone
    {
        get => Pad.RightDeadZonePercent;
        set => SetDeadZone(value, Pad.RightDeadZonePercent, v => Pad.RightDeadZonePercent = v, nameof(RightDeadZoneText));
    }

    public string LeftDeadZoneText => $"{Pad.LeftDeadZonePercent} %";
    public string RightDeadZoneText => $"{Pad.RightDeadZonePercent} %";
    public static double MaxDeadZone => GamepadSettings.MaxDeadZonePercent;

    private void SetDeadZone(double value, int current, Action<int> apply, string textName,
        [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        var percent = Math.Clamp((int)Math.Round(value), 0, GamepadSettings.MaxDeadZonePercent);
        if (percent == current) return;
        apply(percent);
        Save();
        OnPropertyChanged(name);
        OnPropertyChanged(textName);
    }


    public bool OwnLightColor
    {
        get => Pad.OwnLightColor;
        set
        {
            if (Pad.OwnLightColor == value) return;
            Pad.OwnLightColor = value;
            Save();
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<LightSwatch> Swatches { get; } =
    [
        new("Blood", "#B3141E"), new("Gold", "#C9A24A"), new("Moon", "#7FB2DC"), new("Insight", "#7B52C4"),
        new("Beast", "#D9782C"), new("Ash", "#E8E4DC"), new("Off", "#000000"),
    ];

    [ObservableProperty] public partial bool IsCustomColor { get; set; }

    [RelayCommand]
    private void PickSwatch(LightSwatch swatch)
    {
        IsCustomColor = false;
        (_red, _green, _blue) = PadButtons.ParseColor(swatch.Color);
        SetColor();
    }

    [RelayCommand]
    private void PickCustom() => IsCustomColor = true;

    private byte _red, _green, _blue;

    public double Red { get => _red; set => SetChannel(ref _red, value); }
    public double Green { get => _green; set => SetChannel(ref _green, value); }
    public double Blue { get => _blue; set => SetChannel(ref _blue, value); }

    public IBrush CustomBrush => new SolidColorBrush(Avalonia.Media.Color.FromRgb(_red, _green, _blue));

    private void SetChannel(ref byte channel, double value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        var b = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
        if (channel == b) return;
        channel = b;
        OnPropertyChanged(name);
        SetColor();
    }

    private void SetColor()
    {
        var color = PadButtons.FormatColor(_red, _green, _blue);
        if (Pad.LightColor != color)
        {
            Pad.LightColor = color;
            Save();
        }
        var match = Swatches.FirstOrDefault(s => s.Color == color);
        if (match is null) IsCustomColor = true;
        foreach (var swatch in Swatches) swatch.IsSelected = !IsCustomColor && swatch == match;
        OnPropertyChanged(nameof(Red));
        OnPropertyChanged(nameof(Green));
        OnPropertyChanged(nameof(Blue));
        OnPropertyChanged(nameof(CustomBrush));
    }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGameStopped))]
    public partial bool IsGameRunning { get; set; }

    public bool IsGameStopped => !IsGameRunning;

    [RelayCommand]
    private void OpenConfigFolder()
    {
        var profile = EmulatorProfile.Default;
        profile.EnsureCreated(seedFromUserDir: PlatformServices.Current.GetShadPs4UserDir());
        var path = KbmLayout.ConfigPath(profile.UserDir, Bloodborne.TitleId);
        if (!File.Exists(path)) KbmLayout.Write(profile.UserDir, Bloodborne.TitleId, withMod: true, Pad);
        PlatformServices.Current.OpenFolder(Path.GetDirectoryName(path)!);
    }

    private void Save() => SettingsStore.Save(_settings);
}
