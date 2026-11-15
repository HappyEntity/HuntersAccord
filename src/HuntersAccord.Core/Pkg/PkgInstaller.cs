namespace HuntersAccord.Core.Pkg;

/// <summary>What a package contains, readable without keys.</summary>
public sealed record PkgInfo(string Path, string ContentId, string TitleId, string? Title, string? AppVersion, bool IsPatch, long PfsSize)
{
    /// <summary>Folder name shadPS4 expects: the title ID for the game, <c>&lt;TITLE_ID&gt;-UPDATE</c> for a patch.</summary>
    public string InstallFolderName => IsPatch ? $"{TitleId}-UPDATE" : TitleId;
}

public readonly record struct PkgInstallProgress(long BytesDone, long BytesTotal, int FilesDone, int FilesTotal)
{
    public double Fraction => BytesTotal == 0 ? 0 : (double)BytesDone / BytesTotal;
}

/// <summary>Unpacks a PS4 package into the folder layout shadPS4 loads games from.</summary>
public static class PkgInstaller
{
    private const string MarkerFile = ".ha-install";

    private const uint FirstSystemFileEntry = 0x1000;

    public static PkgInfo Inspect(string pkgPath)
    {
        using var pkg = PkgFile.Open(pkgPath);
        var sfo = pkg.ReadParamSfo();
        return new PkgInfo(pkgPath, pkg.ContentId, sfo.TitleId ?? pkg.TitleId, sfo.Title, sfo.AppVersion, sfo.IsPatch, pkg.PfsImageSize);
    }

    /// <summary>True when <paramref name="pkgPath"/> was already fully unpacked into <paramref name="destinationRoot"/>.</summary>
    public static bool IsInstalled(PkgInfo info, string destinationRoot)
    {
        var folder = Path.Combine(destinationRoot, info.InstallFolderName);
        var marker = Path.Combine(folder, MarkerFile);
        return File.Exists(marker)
               && File.ReadAllText(marker).Trim() == Stamp(info);
    }

    /// <summary>
    /// Unpacks the package into <c>&lt;destinationRoot&gt;/&lt;InstallFolderName&gt;</c>. Work happens in a
    /// <c>.partial</c> folder that is renamed only on success, so an interrupted install never looks complete.
    /// </summary>
    public static async Task<string> InstallAsync(
        string pkgPath, string destinationRoot, PkgKeys keys,
        IProgress<PkgInstallProgress>? progress = null, CancellationToken ct = default)
    {
        using var pkg = PkgFile.Open(pkgPath);
        var sfo = pkg.ReadParamSfo();
        var info = new PkgInfo(pkgPath, pkg.ContentId, sfo.TitleId ?? pkg.TitleId, sfo.Title, sfo.AppVersion, sfo.IsPatch, pkg.PfsImageSize);

        var target = Path.Combine(destinationRoot, info.InstallFolderName);
        var partial = target + ".partial";

        var ekpfs = PkgCrypto.DeriveEkpfs(pkg, keys);
        var outer = new PfsImage(pkg, ekpfs);
        var image = PfscImage.Locate(outer);

        PfsFileSystem fs;
        using (var reader = new PfscReader(image))
            fs = PfsFileSystem.Load(reader);

        var total = fs.Files.Sum(f => f.Size);
        Directory.CreateDirectory(destinationRoot);
        var free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(destinationRoot))!).AvailableFreeSpace;
        var existing = Directory.Exists(target) ? DirectorySize(target) : 0; // replaced on success
        if (free + existing < total + SpaceReserve)
            throw new NotEnoughSpaceException(total + SpaceReserve - existing, free);

        if (Directory.Exists(partial)) Directory.Delete(partial, recursive: true);
        Directory.CreateDirectory(partial);
        foreach (var dir in fs.Directories)
            Directory.CreateDirectory(SafeCombine(partial, dir));

        long done = 0;
        var filesDone = 0;
        progress?.Report(new PkgInstallProgress(0, total, 0, fs.Files.Count));

        try
        {
            await ExtractAllAsync();
        }
        catch (OperationCanceledException)
        {
            TryDelete(partial);
            throw;
        }

        async Task ExtractAllAsync() => await Task.Run(() =>
        {
            using var readers = new ThreadLocal<PfscReader>(() => new PfscReader(image), trackAllValues: true);
            try
            {
                // Largest files first so the long ones overlap with the many small ones.
                Parallel.ForEach(
                    fs.Files.OrderByDescending(f => f.Size),
                    new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount },
                    file =>
                    {
                        ExtractFile(readers.Value!, file, SafeCombine(partial, file.RelativePath), bytes =>
                        {
                            var now = Interlocked.Add(ref done, bytes);
                            progress?.Report(new PkgInstallProgress(now, total, Volatile.Read(ref filesDone), fs.Files.Count));
                        }, ct);
                        Interlocked.Increment(ref filesDone);
                    });
            }
            finally
            {
                foreach (var r in readers.Values) r.Dispose();
            }
        }, ct);

        // System files kept as package entries (param.sfo, icons, trophies...) go to sce_sys.
        foreach (var entry in pkg.Entries.Where(e => e.Id >= FirstSystemFileEntry && e.Name is not null && !e.IsEncrypted))
        {
            var path = SafeCombine(Path.Combine(partial, "sce_sys"), entry.Name!);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, pkg.ReadEntry(entry), ct);
        }

        await File.WriteAllTextAsync(Path.Combine(partial, MarkerFile), Stamp(info), ct);

        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        Directory.Move(partial, target);
        progress?.Report(new PkgInstallProgress(total, total, fs.Files.Count, fs.Files.Count));
        return target;
    }

    private static void ExtractFile(PfscReader reader, PfsFile file, string path, Action<long> onBytes, CancellationToken ct)
    {
        const int chunk = 4 * 1024 * 1024;
        using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
        output.SetLength(file.Size);

        var buffer = new byte[(int)Math.Min(chunk, Math.Max(file.Size, 1))];
        for (long pos = 0; pos < file.Size; pos += buffer.Length)
        {
            ct.ThrowIfCancellationRequested();
            var n = (int)Math.Min(buffer.Length, file.Size - pos);
            reader.Read(file.Offset + pos, buffer.AsSpan(0, n));
            output.Write(buffer, 0, n);
            onBytes(n);
        }
    }

    /// <summary>Joins a package-relative path, refusing anything that would escape the target folder.</summary>
    private static string SafeCombine(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Package path escapes the install folder: {relative}");
        return full;
    }

    private static string Stamp(PkgInfo info) => $"{info.ContentId} {info.AppVersion}";

    /// <summary>Head-room left on the drive after unpacking (shader cache, saves, logs).</summary>
    private const long SpaceReserve = 1L << 30;

    private static long DirectorySize(string dir) =>
        new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

public sealed class NotEnoughSpaceException(long required, long available)
    : IOException($"Not enough disk space: {required / 1073741824.0:F1} GiB required, {available / 1073741824.0:F1} GiB free.")
{
    public long Required { get; } = required;
    public long Available { get; } = available;
}
