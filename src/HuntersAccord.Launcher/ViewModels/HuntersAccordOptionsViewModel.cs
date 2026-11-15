using HuntersAccord.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HuntersAccord.Launcher.ViewModels;

/// <summary>Hunters' Accord's own features in the Patches section, saved in the game settings the mod reads; locked while the game runs.</summary>
public sealed partial class HuntersAccordOptionsViewModel : ObservableObject
{
    private bool _loading;

    public HuntersAccordOptionsViewModel() => Load();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleSub))]
    [NotifyPropertyChangedFor(nameof(CanToggleIcons))]
    public partial bool KeyboardMouse { get; set; }

    [ObservableProperty] public partial bool MouseCamera { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleIcons))]
    public partial bool KeyPrompts { get; set; }

    [ObservableProperty] public partial bool KeyIcons { get; set; }


    [ObservableProperty] public partial bool SkipLogos { get; set; }


    [ObservableProperty] public partial bool LampRest { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditScaling))]
    public partial bool CoopScaling { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaleEnemyHpText))]
    public partial int ScaleEnemyHp { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaleEnemyDamageText))]
    public partial int ScaleEnemyDamage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaleBossHpText))]
    public partial int ScaleBossHp { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaleBossDamageText))]
    public partial int ScaleBossDamage { get; set; }

    public string ScaleEnemyHpText => $"+{ScaleEnemyHp} %";
    public string ScaleEnemyDamageText => $"+{ScaleEnemyDamage} %";
    public string ScaleBossHpText => $"+{ScaleBossHp} %";
    public string ScaleBossDamageText => $"+{ScaleBossDamage} %";

    public bool CanEditScaling => IsEditable && CoopScaling;

    /// <summary>False while the game runs (the mod keeps its own copy of the settings and would write it back).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleSub))]
    [NotifyPropertyChangedFor(nameof(CanToggleIcons))]
    [NotifyPropertyChangedFor(nameof(CanEditScaling))]
    public partial bool IsEditable { get; set; } = true;

    public bool CanToggleSub => IsEditable && KeyboardMouse;

    public bool CanToggleIcons => CanToggleSub && KeyPrompts;

    public void Load()
    {
        var settings = GameSettings.Load();
        _loading = true;
        KeyboardMouse = settings.KeyboardMouse;
        MouseCamera = settings.MouseCamera;
        KeyPrompts = settings.KeyPrompts;
        KeyIcons = settings.KeyIcons;
        SkipLogos = settings.SkipLogos;
        LampRest = settings.LampRest;
        CoopScaling = settings.CoopScaling;
        ScaleEnemyHp = settings.ScaleEnemyHp;
        ScaleEnemyDamage = settings.ScaleEnemyDamage;
        ScaleBossHp = settings.ScaleBossHp;
        ScaleBossDamage = settings.ScaleBossDamage;
        _loading = false;
    }

    partial void OnIsEditableChanged(bool value)
    {
        if (value) Load();
    }

    partial void OnKeyboardMouseChanged(bool value) => Save(s => s.KeyboardMouse = value);

    partial void OnMouseCameraChanged(bool value) => Save(s => s.MouseCamera = value);

    partial void OnKeyPromptsChanged(bool value) => Save(s => s.KeyPrompts = value);

    partial void OnKeyIconsChanged(bool value) => Save(s => s.KeyIcons = value);


    partial void OnSkipLogosChanged(bool value) => Save(s => s.SkipLogos = value);


    partial void OnLampRestChanged(bool value) => Save(s => s.LampRest = value);

    partial void OnCoopScalingChanged(bool value) => Save(s => s.CoopScaling = value);

    partial void OnScaleEnemyHpChanged(int value) => Save(s => s.ScaleEnemyHp = value);

    partial void OnScaleEnemyDamageChanged(int value) => Save(s => s.ScaleEnemyDamage = value);

    partial void OnScaleBossHpChanged(int value) => Save(s => s.ScaleBossHp = value);

    partial void OnScaleBossDamageChanged(int value) => Save(s => s.ScaleBossDamage = value);

    private void Save(Action<GameSettings> change)
    {
        if (_loading) return;
        var settings = GameSettings.Load();
        change(settings);
        settings.Save();
    }
}
