using System.IO.Compression;
using HuntersAccord.Core.Diagnostics;

namespace HuntersAccord.Core.Tests;

public sealed class BugReportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ha-report-" + Guid.NewGuid().ToString("N"));

    public BugReportTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Touch(string relative, string text = "x")
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void File_name_has_the_time() =>
        Assert.Equal("HuntersAccord-report-20260102-030405.zip", BugReport.FileNameFor(new DateTime(2026, 1, 2, 3, 4, 5)));

    [Fact]
    public void Only_the_newest_dump_is_chosen_by_its_time_stamp()
    {
        var names = new[] { "crash-20260101-120000.dmp", "crash-20260301-080000.dmp", "crash-20260201-235959.dmp", "crash-notes.dmp", "x.dmp" };
        Assert.Equal("crash-20260301-080000.dmp", BugReport.NewestDump(names));
        Assert.Null(BugReport.NewestDump(["ha-mod.log", "crash-x.dmp"]));
    }

    [Fact]
    public void Short_files_are_read_whole_and_long_ones_from_the_last_twenty_megabytes()
    {
        Assert.Equal((0L, 100L), BugReport.ReadRange(100));
        Assert.Equal((0L, BugReport.TailThreshold), BugReport.ReadRange(BugReport.TailThreshold));
        var length = BugReport.TailThreshold + 1;
        Assert.Equal((length - BugReport.TailBytes, BugReport.TailBytes), BugReport.ReadRange(length));
    }

    [Fact]
    public void Selection_takes_the_logs_the_newest_dump_with_its_guest_memory_and_the_settings_but_nothing_else()
    {
        Touch("logs/ha-mod.log");
        Touch("logs/ha-mod.old.log");
        Touch("logs/ha-mod-2.log");
        Touch("logs/crash-20260101-000000.dmp");
        Touch("logs/crash-20260101-000000.guest.bin");
        Touch("logs/crash-20260202-000000.dmp");
        Touch("logs/crash-20260202-000000.guest.bin");
        Touch("profile/user/log/shad_log.txt");
        Touch("profile/user/log/other.txt");
        Touch("profile/user/savedata/save.sav");
        var settings = Touch("settings.json");
        var keys = Touch("keys.json");
        var files = BugReport.Select(Path.Combine(_root, "logs"), Path.Combine(_root, "profile", "user", "log"),
            [settings, Path.Combine(_root, "game-settings.json"), keys]); // keys.json never goes in, even when asked

        Assert.Equal(
            ["logs/ha-mod.log", "logs/ha-mod.old.log", "logs/crash-20260202-000000.dmp", "logs/crash-20260202-000000.guest.bin",
             "emulator/shad_log.txt", "settings/settings.json"],
            files.Select(f => f.EntryName));
    }

    [Fact]
    public void Missing_folders_give_an_empty_selection() =>
        Assert.Empty(BugReport.Select(Path.Combine(_root, "none"), Path.Combine(_root, "none2"), []));

    [Fact]
    public void Zip_holds_info_and_the_files_and_cuts_a_huge_log_to_its_tail()
    {
        var small = Touch("logs/ha-mod.log", "hello");
        var big = Path.Combine(_root, "shad_log.txt");
        using (var f = File.Create(big))
        {
            f.SetLength(BugReport.TailThreshold + 10);
            f.Seek(-4, SeekOrigin.End);
            f.Write("TAIL"u8);
        }
        var zipPath = Path.Combine(_root, "out", "r.zip");
        BugReport.Create(zipPath, [new ReportFile(small, "logs/ha-mod.log"), new ReportFile(big, "emulator/shad_log.txt")], "info text");

        using var zip = ZipFile.OpenRead(zipPath);
        Assert.Equal(["info.txt", "logs/ha-mod.log", "emulator/shad_log.txt"], zip.Entries.Select(e => e.FullName));
        using (var r = new StreamReader(zip.GetEntry("logs/ha-mod.log")!.Open())) Assert.Equal("hello", r.ReadToEnd());
        var tail = zip.GetEntry("emulator/shad_log.txt")!;
        Assert.True(tail.Length < BugReport.TailBytes + 200);
        using var stream = tail.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        Assert.EndsWith("TAIL", System.Text.Encoding.UTF8.GetString(ms.ToArray()));
    }

    [Fact]
    public void A_failed_zip_leaves_no_file()
    {
        var zipPath = Path.Combine(_root, "r.zip");
        Assert.ThrowsAny<IOException>(() => BugReport.Create(zipPath, [new ReportFile(Path.Combine(_root, "gone.log"), "logs/gone.log")], ""));
        Assert.False(File.Exists(zipPath));
    }
}
