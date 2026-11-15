using HuntersAccord.Core.Trophies;

namespace HuntersAccord.Launcher.Services;

/// <summary>A trophy as the launcher shows it: the list's entry and, once earned, when / with whom / how.</summary>
public sealed record TrophyRow(TrophyInfo Info, TrophyEntry? Earned)
{
    public bool IsEarned => Earned is not null;

    /// <summary>A hidden trophy not earned yet: its name and description not shown ("???").</summary>
    public bool IsSecret => Info.Hidden && Earned is null;
}

/// <summary>The progress at a glance: earned of all, by grade, the platinum.</summary>
public sealed record TrophyProgress(int Earned, int Total, int Bronze, int Silver, int Gold, bool Platinum, TrophyEntry? Latest)
{
    public double Percent => Total == 0 ? 0 : 100.0 * Earned / Total;
}

/// <summary>
/// A player's own trophies for the launcher: its book (trophies.json, written by the game's mod) read and followed while
/// the launcher runs (<see cref="Changed"/> on any thread when the file is written). Read only: the mod alone writes it.
/// The page that shows them comes with the design; this is its model.
/// </summary>
public sealed class TrophyService : IDisposable
{
    private readonly string _path;
    private FileSystemWatcher? _watcher;
    private TrophyBook _book;

    public TrophyService(int player = 1)
        : this(TrophyBook.FileFor(player))
    {
    }

    public TrophyService(string path)
    {
        _path = path;
        _book = TrophyBook.Read(path);
    }

    /// <summary>The book was written again (and read here): any thread.</summary>
    public event EventHandler? Changed;

    public string FilePath => _path;

    /// <summary>Every trophy in the game's order (the game's own words when the trophy key opens them), with its earned entry when held.</summary>
    public IReadOnlyList<TrophyRow> Rows()
    {
        var book = _book;
        return [.. TrophyTextSource.Instance.All.Select(t => new TrophyRow(t, book.Get(t.Id)))];
    }

    public TrophyProgress Progress()
    {
        var rows = Rows();
        int Count(TrophyGrade grade) => rows.Count(r => r.IsEarned && r.Info.Grade == grade);
        var latest = rows.Where(r => r.Earned?.Earned is not null).Select(r => r.Earned!).MaxBy(e => e.Earned);
        return new TrophyProgress(rows.Count(r => r.IsEarned), rows.Count, Count(TrophyGrade.Bronze), Count(TrophyGrade.Silver),
            Count(TrophyGrade.Gold), rows.Any(r => r.IsEarned && r.Info.Grade == TrophyGrade.Platinum), latest);
    }

    /// <summary>Read again now.</summary>
    public void Reload() => _book = TrophyBook.Read(_path);

    /// <summary>Follows the file: read again and <see cref="Changed"/> whenever the mod writes it.</summary>
    public void Watch()
    {
        if (_watcher is not null) return;
        var folder = Path.GetDirectoryName(Path.GetFullPath(_path))!;
        Directory.CreateDirectory(folder);
        _watcher = new FileSystemWatcher(folder, TrophyBook.FileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
        };
        _watcher.Changed += OnFile;
        _watcher.Created += OnFile;
        _watcher.Renamed += OnFile; // the mod writes beside it and moves it over
        _watcher.EnableRaisingEvents = true;
    }

    private void OnFile(object sender, FileSystemEventArgs e)
    {
        Reload();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _watcher?.Dispose();
}
