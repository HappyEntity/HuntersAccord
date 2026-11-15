using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using HuntersAccord.Launcher.ViewModels;

namespace HuntersAccord.Launcher.Views;

/// <summary>The launcher's only window: tabs Play · Patches · Trophies · Settings in the title bar, one page under it.</summary>
public partial class MainWindow : Window
{
    private const double SceneWidth = 900, SceneHeight = 560;
    private const int PlayTab = 0, PatchesTab = 1, TrophiesTab = 2, SettingsTab = 3;

    /// <summary>Narrower than this, the pages take the 720x480 layout (labels over controls, a narrower column).</summary>
    private const double NarrowBelow = 800;

    private static readonly TimeSpan FadeIn = TimeSpan.FromMilliseconds(120);

    public MainWindow()
    {
        InitializeComponent();
        Tabs.SelectionChanged += OnTabChanged;
        // The backdrop scene covers the window at any size and stays anchored to the bottom (the city on the edge).
        SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < NarrowBelow;
            Root.Classes.Set("narrow", narrow);
            var scale = Math.Max(e.NewSize.Width / SceneWidth, e.NewSize.Height / SceneHeight);
            Scene.RenderTransform = new ScaleTransform(scale, scale);
        };
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    public void ShowPatches() => Tabs.SelectedIndex = PatchesTab;

    public void ShowTrophies() => Tabs.SelectedIndex = TrophiesTab;

    public void ShowSteamSettings()
    {
        SettingsView.ShowSteam();
        Tabs.SelectedIndex = SettingsTab;
    }

    private void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        var tab = Tabs.SelectedIndex;
        if (ViewModel is { } vm)
        {
            if (tab != PatchesTab) vm.Patches.ShowFeaturedDetails = false; // the sections again next time
            if (tab == PatchesTab) PreparePatches(vm);
            if (tab == SettingsTab) PrepareSettings(vm);
            if (tab == TrophiesTab) vm.Trophies.Refresh();
        }
        // Patches and Settings are working pages: night over the title screen, a hairline under the tabs.
        PageVeil.IsVisible = TitleLine.IsVisible = tab != PlayTab;
        Show(PlayView, tab == PlayTab);
        Show(PatchesView, tab == PatchesTab);
        Show(TrophiesView, tab == TrophiesTab);
        Show(SettingsView, tab == SettingsTab);
    }

    private static void Show(Control page, bool visible)
    {
        if (page.IsVisible == visible) return;
        page.IsVisible = visible;
        if (!visible) return;
        page.Transitions = null;
        page.Opacity = 0;
        page.Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = FadeIn }];
        page.Opacity = 1;
    }

    private static void PreparePatches(MainWindowViewModel vm)
    {
        vm.Patches.Game = vm.Game.SelectedInstall?.Install;
        vm.Patches.RefreshKeys();
    }

    private void PrepareSettings(MainWindowViewModel vm)
    {
        vm.Backups.Refresh();
        if ((Screens.ScreenFromWindow(this) ?? Screens.Primary) is { } screen)
            vm.Display.LimitToScreen(screen.Bounds.Width, screen.Bounds.Height);
        vm.Display.RefreshHotkeys();
        _ = vm.Display.RefreshCacheAsync();
    }

    /// <summary>Esc: from the 60 FPS (Hunters' Accord) page back to the patches, from another tab back to Play.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Escape) return;
        if (Tabs.SelectedIndex == PatchesTab && ViewModel is { Patches.ShowFeaturedDetails: true } vm)
            vm.Patches.ShowFeaturedDetails = false;
        else if (Tabs.SelectedIndex != PlayTab)
            Tabs.SelectedIndex = PlayTab;
        else
            return;
        e.Handled = true;
    }

    private void OnTitlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private void OnMinimize(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseWindow(object? sender, RoutedEventArgs e) => Close();
}
