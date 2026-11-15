using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace HuntersAccord.Core.Diagnostics;

/// <summary>One file of a bug report: where it is read from and under what name it goes into the zip.</summary>
/// <param name="Source">Full path of the file on disk.</param>
/// <param name="EntryName">Name inside the zip (forward slashes).</param>
public sealed record ReportFile(string Source, string EntryName);

/// <summary>
/// The zip a player attaches to a bug report: the mod's and the emulator's logs, the newest crash dump, the launcher's
/// settings and a short info.txt. Never saves, keys or game files. Reads only; the only thing written is the zip.
/// </summary>
public static partial class BugReport
{
    /// <summary>A log longer than this is cut to its last <see cref="TailBytes"/>.</summary>
    public const long TailThreshold = 50L * 1024 * 1024;

    public const long TailBytes = 20L * 1024 * 1024;

    public static string FileNameFor(DateTime now) => $"HuntersAccord-report-{now:yyyyMMdd-HHmmss}.zip";

    /// <summary>The newest crash dump among <paramref name="fileNames"/> (<c>crash-yyyyMMdd-HHmmss.dmp</c>, or <c>crash-2-…</c> for the second player); null if none.</summary>
    public static string? NewestDump(IEnumerable<string> fileNames) =>
        fileNames.Select(f => (Name: f, Match: DumpName().Match(Path.GetFileName(f))))
            .Where(x => x.Match.Success)
            .OrderByDescending(x => x.Match.Groups["stamp"].Value, StringComparer.Ordinal)
            .Select(x => x.Name).FirstOrDefault();

    /// <summary>Where to start reading a file of <paramref name="length"/> bytes, and how many bytes to take.</summary>
    public static (long Start, long Count) ReadRange(long length) =>
        length > TailThreshold ? (length - TailBytes, TailBytes) : (0, length);

    /// <summary>The files that go into the report, in order. Missing ones are left out.</summary>
    /// <param name="logsDir">The mod's logs folder.</param>
    /// <param name="emulatorLogDir">The emulator profile's log folder (shad_log.txt).</param>
    /// <param name="settingsFiles">Launcher and game settings files.</param>
    public static IReadOnlyList<ReportFile> Select(string logsDir, string emulatorLogDir, IEnumerable<string> settingsFiles)
    {
        var files = new List<ReportFile>();
        void Add(string path, string folder)
        {
            // Never the keys (keys.json: PKG and trophy keys), whatever list it came in.
            if (Path.GetFileName(path).Equals(Pkg.PkgKeys.FileName, StringComparison.OrdinalIgnoreCase)) return;
            if (File.Exists(path)) files.Add(new ReportFile(path, folder + "/" + Path.GetFileName(path)));
        }

        Add(Path.Combine(logsDir, "ha-mod.log"), "logs");
        Add(Path.Combine(logsDir, "ha-mod.old.log"), "logs");
        if (Directory.Exists(logsDir) &&
            NewestDump(Directory.EnumerateFiles(logsDir, "crash*.dmp")) is { } dump)
        {
            Add(dump, "logs");
            Add(Path.ChangeExtension(dump, ".guest.bin"), "logs");
        }
        Add(Path.Combine(emulatorLogDir, "shad_log.txt"), "emulator");
        Add(Path.Combine(emulatorLogDir, "shad_log.old.txt"), "emulator");
        foreach (var settings in settingsFiles) Add(settings, "settings");
        return files;
    }

    /// <summary>Writes the zip: info.txt and <paramref name="files"/> (long logs cut to their end). No half-written zip is left on failure.</summary>
    public static string Create(string zipPath, IReadOnlyList<ReportFile> files, string info)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zipPath))!);
        try
        {
            using (var stream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var infoEntry = zip.CreateEntry("info.txt", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(infoEntry.Open(), new UTF8Encoding(false)))
                    writer.Write(info);
                foreach (var file in files) AddFile(zip, file);
            }
        }
        catch
        {
            try { File.Delete(zipPath); } catch (IOException) { }
            throw;
        }
        return zipPath;
    }

    private static void AddFile(ZipArchive zip, ReportFile file)
    {
        // The game may be running and writing the log: read it without locking it out.
        using var source = new FileStream(file.Source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var (start, count) = ReadRange(source.Length);
        var entry = zip.CreateEntry(file.EntryName, CompressionLevel.Optimal);
        using var target = entry.Open();
        if (start > 0)
        {
            target.Write(Encoding.UTF8.GetBytes($"[... the first {start} bytes are cut, the last {count} are kept ...]\n"));
            source.Seek(start, SeekOrigin.Begin);
        }
        var buffer = new byte[81920];
        var left = count;
        while (left > 0)
        {
            var read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, left));
            if (read <= 0) break; // the file shrank (a log started over)
            target.Write(buffer, 0, read);
            left -= read;
        }
    }

    [GeneratedRegex(@"^crash(-\d+)?-(?<stamp>\d{8}-\d{6})\.dmp$", RegexOptions.IgnoreCase)]
    private static partial Regex DumpName();
}
