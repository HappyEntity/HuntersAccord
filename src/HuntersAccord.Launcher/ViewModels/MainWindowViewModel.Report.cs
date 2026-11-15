using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HuntersAccord.Core;
using HuntersAccord.Core.Diagnostics;
using HuntersAccord.Core.Emulator;
using HuntersAccord.Core.Settings;
using HuntersAccord.Launcher.Localization;
using HuntersAccord.Launcher.Services;
using HuntersAccord.Platform;

namespace HuntersAccord.Launcher.ViewModels;

/// <summary>The bug report button (Settings → About, under "If something broke"; and Home after an error).</summary>
public sealed partial class MainWindowViewModel
{
    public LocText ReportStatus { get; } = new("Report.Working");

    [ObservableProperty] public partial bool HasReportStatus { get; set; }

    /// <summary>The last report's zip (shown with "Show in folder"); null before one or when it failed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReportPath))]
    public partial string? ReportPath { get; set; }

    public bool HasReportPath => ReportPath is not null;

    [RelayCommand]
    private void ShowReportInFolder()
    {
        if (ReportPath is not { } path) return;
        try { PlatformServices.Current.RevealFile(path); }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MakeReportCommand))]
    public partial bool IsMakingReport { get; set; }

    /// <summary>The zip goes next to the launcher (else Documents, else the data folder) and is shown in the file manager.</summary>
    [RelayCommand(CanExecute = nameof(CanMakeReport))]
    private async Task MakeReportAsync()
    {
        IsMakingReport = true;
        ReportStatus.Set("Report.Working");
        HasReportStatus = true;
        ReportPath = null;
        try
        {
            var info = BuildReportInfo();
            var zipPath = Path.Combine(ReportFolder(), BugReport.FileNameFor(DateTime.Now));
            await Task.Run(() =>
            {
                var files = BugReport.Select(AppPaths.LogsDir, Path.GetDirectoryName(_profile.LogFile)!,
                    [AppPaths.SettingsFile, GameSettings.FilePath]);
                BugReport.Create(zipPath, files, info);
            });
            ReportStatus.Set("Report.SavedShort");
            ReportPath = zipPath;
            try { PlatformServices.Current.RevealFile(zipPath); }
            catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException) { }
        }
        catch (Exception e)
        {
            ReportStatus.Set("Report.Failed", FriendlyErrors.Describe(e));
        }
        finally
        {
            IsMakingReport = false;
        }
    }

    private bool CanMakeReport() => !IsMakingReport;

    private static string ReportFolder()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        foreach (var folder in new[] { AppContext.BaseDirectory, documents })
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder) && Writable(folder)) return folder;
        return AppPaths.DataRoot;
    }

    // A launcher unpacked into Program Files can't write next to itself.
    private static bool Writable(string folder)
    {
        try
        {
            var probe = Path.Combine(folder, $".ha-write-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>info.txt: versions and the choices that change how the game runs. No keys, no accounts.</summary>
    private string BuildReportInfo()
    {
        var text = new StringBuilder();
        text.AppendLine($"Report time: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        text.AppendLine($"Launcher: {VersionNumber}");
        text.AppendLine($"Mod: {ModVersion()}");
        text.AppendLine($"shadPS4: {(_installed is { } local ? ShadPs4Release.DisplayNameOf(local.Tag) : "not installed")} ({SelectedChannel.Value} channel)");
        text.AppendLine($"OS: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}");
        text.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
        text.AppendLine($"Launcher language: {_settings.Language}; game language: {_settings.GameLanguage?.ToString() ?? "auto"}");
        text.AppendLine($"Game: {GameInfo}; copies found: {Game.Installs.Count}; started without the mod: {LaunchWithoutMod}");
        text.AppendLine($"Co-op network: {_settings.CoopNetwork}; data folder moved: {AppPaths.IsDataRootMoved}");
        text.AppendLine($"Active patches: {string.Join(", ", Patches.Active().Select(p => p.Key).DefaultIfEmpty("none"))}");
        return text.ToString();
    }

    private static string ModVersion()
    {
        if (GameLauncher.FindModLayerDir() is not { } dir) return "not present";
        var library = Path.Combine(dir, ModLibrary.FileName);
        if (!File.Exists(library)) return "layer without its library";
        var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(library);
        return version.ProductVersion ?? version.FileVersion ?? "unknown";
    }
}
