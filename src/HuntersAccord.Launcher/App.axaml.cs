using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HuntersAccord.Core.Settings;
using HuntersAccord.Launcher.Localization;
using HuntersAccord.Launcher.Services;
using HuntersAccord.Launcher.ViewModels;
using HuntersAccord.Launcher.Views;

namespace HuntersAccord.Launcher;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = SettingsStore.Load();
            Localizer.Instance.SetLanguage(settings.Language);
            UiSounds.Enabled = settings.UiSounds;
            UiSounds.Attach();

            var window = new MainWindow();
            var vm = new MainWindowViewModel(settings, new AvaloniaStorageService(window));
            window.DataContext = vm;
            desktop.MainWindow = window;
            _ = vm.InitializeAsync();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
