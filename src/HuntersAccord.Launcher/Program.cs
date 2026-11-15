using Avalonia;
using Avalonia.Media;

namespace HuntersAccord.Launcher;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the IDE previewer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Text defaults to the theme's EB Garamond, also in pop-ups and tool tips (no Inter font package).
            .With(new FontManagerOptions { DefaultFamilyName = "avares://HuntersAccord/Assets/Fonts#EB Garamond" })
            .LogToTrace();
}
