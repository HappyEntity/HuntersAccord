using Avalonia.Controls;

namespace HuntersAccord.Launcher.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage() => InitializeComponent();

    public void ShowSteam() => SteamTab.IsSelected = true;
}
