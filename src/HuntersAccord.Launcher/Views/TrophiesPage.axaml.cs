using Avalonia.Controls;
using Avalonia.Input;
using HuntersAccord.Launcher.ViewModels;

namespace HuntersAccord.Launcher.Views;

/// <summary>The Trophies tab: the filters on the left, the header with the platinum and the progress, the list.</summary>
public partial class TrophiesPage : UserControl
{
    public TrophiesPage() => InitializeComponent();

    /// <summary>A click on a hidden trophy's row shows it.</summary>
    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is TrophyRowViewModel row) row.Reveal();
    }
}
