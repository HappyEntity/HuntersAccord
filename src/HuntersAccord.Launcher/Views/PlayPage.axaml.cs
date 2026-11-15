using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using HuntersAccord.Launcher.ViewModels;

namespace HuntersAccord.Launcher.Views;

/// <summary>The Play tab: logo, Play and one status line; the versions and the patches in use in the bottom corners.</summary>
public partial class PlayPage : UserControl
{
    public PlayPage() => InitializeComponent();

    private MainWindow? Window => TopLevel.GetTopLevel(this) as MainWindow;

    private void OnVersionPressed(object? sender, PointerPressedEventArgs e) =>
        (DataContext as MainWindowViewModel)?.Patches.CountDeveloperClick();

    private void OnOpenPatches(object? sender, RoutedEventArgs e) => Window?.ShowPatches();

    private void OnOpenTrophies(object? sender, RoutedEventArgs e) => Window?.ShowTrophies();

    private void OnOpenSteamSettings(object? sender, RoutedEventArgs e) => Window?.ShowSteamSettings();
}
