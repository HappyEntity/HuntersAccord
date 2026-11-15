using Avalonia.Controls;
using Avalonia.Interactivity;
using HuntersAccord.Launcher.Localization;
using HuntersAccord.Launcher.Services;
using HuntersAccord.Launcher.ViewModels;

namespace HuntersAccord.Launcher.Views;

/// <summary>The Patches tab: the Hunters' Accord section, then the community patches by section.</summary>
public partial class PatchesPage : UserControl
{
    public PatchesPage() => InitializeComponent();

    /// <summary>The rail's heading is an item of the list but no section: it can't be chosen and has no hover.</summary>
    private void OnRailContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        var heading = e.Container.DataContext is PatchRailHeading;
        e.Container.IsEnabled = !heading;
        e.Container.Focusable = !heading;
        e.Container.Classes.Set("heading", heading);
    }

    private async void OnInstallDebugFont(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PatchesViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        var file = await new AvaloniaStorageService(top).PickFileAsync(
            Localizer.Instance["Patches.FontPickTitle"], Localizer.Instance["Patches.FontFileType"], "*.zip", "*.ccm");
        if (file is not null) vm.InstallDebugFont(file);
    }
}
