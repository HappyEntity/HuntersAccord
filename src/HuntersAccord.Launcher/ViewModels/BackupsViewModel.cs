using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using HuntersAccord.Core.Saves;
using HuntersAccord.Core.Settings;
using HuntersAccord.Launcher.Localization;
using HuntersAccord.Launcher.Services;
using HuntersAccord.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HuntersAccord.Launcher.ViewModels;

public sealed record BackupChoice(int Value, LocText Label);

public sealed record BackupItem(SaveBackups.Backup Backup)
{
    public string When { get; } = Backup.Time.ToString("g", CultureInfo.CurrentCulture);
    public string? Label => Backup.Label;
    public bool HasLabel => Backup.Label is not null;
}

/// <summary>The save backups of the main profile and how they are made; a copy is put back with the game closed.</summary>
public sealed partial class BackupsViewModel : ObservableObject
{
    private const int Player = 1;

    public BackupsViewModel() => Refresh();

    public ObservableCollection<BackupItem> Items { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand), nameof(DeleteCommand))]
    public partial BackupItem? Selected { get; set; }

    partial void OnSelectedChanged(BackupItem? value) => DeleteArmed = false;

    public LocText Status { get; } = new("Backups.Idle");

    [ObservableProperty] public partial bool HasStatus { get; set; }
    [ObservableProperty] public partial bool HasError { get; set; }

    /// <summary>Delete was pressed once: the second press deletes (a prepared copy is not lost by a stray click).</summary>
    [ObservableProperty] public partial bool DeleteArmed { get; set; }

    private void Report(bool error, string key, params object?[] args)
    {
        Status.Set(key, args);
        HasError = error;
        HasStatus = true;
    }

    public string Key
    {
        get => GameSettings.Load().SaveBackupKey;
        set
        {
            if (value.Length == 0 || value == Key) return;
            Change(s => s.SaveBackupKey = value);
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<BackupChoice> EveryChoices { get; } =
        [new(0, new LocText("Backups.EveryOff")), .. new[] { 5, 10, 15, 30, 60 }.Select(m => new BackupChoice(m, new LocText("Backups.EveryMinutes", m)))];

    public BackupChoice? Every
    {
        get => Pick(EveryChoices, GameSettings.Load().SaveBackupEveryMinutes);
        set { if (value is not null) Change(s => s.SaveBackupEveryMinutes = value.Value); }
    }

    public bool AutoBanner
    {
        get => GameSettings.Load().SaveBackupAutoBanner;
        set
        {
            if (value == AutoBanner) return;
            Change(s => s.SaveBackupAutoBanner = value);
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<BackupChoice> KeptChoices { get; } =
        [.. new[] { 3, 5, 10, 20, 30, 50 }.Select(n => new BackupChoice(n, new LocText("Backups.KeptCount", n)))];

    public BackupChoice? Kept
    {
        get => Pick(KeptChoices, GameSettings.Load().SaveBackupsKept);
        set { if (value is not null) Change(s => s.SaveBackupsKept = value.Value); }
    }

    private static BackupChoice Pick(IReadOnlyList<BackupChoice> choices, int value) =>
        choices.MinBy(c => Math.Abs(c.Value - value))!;

    private void Change(Action<GameSettings> change)
    {
        var settings = GameSettings.Load();
        change(settings);
        settings.Save();
    }

    [RelayCommand]
    public void Refresh()
    {
        var selected = Selected?.Backup.Id;
        Items.Clear();
        foreach (var backup in SaveBackups.List(Player)) Items.Add(new BackupItem(backup));
        Selected = Items.FirstOrDefault(i => i.Backup.Id == selected);
    }

    private bool HasSelected() => Selected is not null;

    /// <summary>Puts the chosen copy back; the saves there now are copied first ("before-restore"), so it can be undone.</summary>
    [RelayCommand(CanExecute = nameof(HasSelected))]
    private async Task RestoreAsync()
    {
        if (Selected?.Backup is not { } backup) return;
        DeleteArmed = false;
        if (Process.GetProcessesByName("shadPS4").Length > 0)
        {
            Report(true, "Backups.GameRunning");
            return;
        }
        try
        {
            await Task.Run(() => SaveBackups.Restore(Player, backup));
            Report(false, "Backups.Restored", new BackupItem(backup).When);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Report(true, "Backups.RestoreFailed", FriendlyErrors.Describe(e));
        }
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(HasSelected))]
    private void Delete()
    {
        if (Selected?.Backup is not { } backup) return;
        if (!DeleteArmed)
        {
            DeleteArmed = true;
            return;
        }
        var index = Items.IndexOf(Selected);
        try
        {
            SaveBackups.Delete(Player, backup);
            Report(false, "Backups.Deleted", new BackupItem(backup).When);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Report(true, "Backups.DeleteFailed", FriendlyErrors.Describe(e));
        }
        Refresh();
        // The next copy is chosen, to delete several one after another.
        Selected = Items.Count == 0 ? null : Items[Math.Min(index, Items.Count - 1)];
    }

    [RelayCommand]
    private void OpenFolder()
    {
        var dir = Path.Combine(SaveBackups.Root, $"player{Player}");
        Directory.CreateDirectory(dir);
        PlatformServices.Current.OpenFolder(dir);
    }
}
