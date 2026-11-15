namespace HuntersAccord.Core.Patches;

/// <summary>Bloodborne's community patches, fetched from the GoldHEN and shadPS4 repositories only when the player presses Update. The mod reads the files and applies the patches the player turned on.</summary>
public static class PatchLibrary
{
    /// <summary>Bloodborne version the patches must be for (Hunters' Accord supports 1.09 only).</summary>
    public const string AppVersion = "01.09";

    private static readonly PatchSource[] Downloaded = [PatchSource.GoldHen, PatchSource.ShadPs4];

    public static string SourceUrl(PatchSource source) => source switch
    {
        PatchSource.GoldHen => "https://raw.githubusercontent.com/illusionyy/PS-Game-Patch/main/patches/xml/Bloodborne-Orbis.xml",
        _ => "https://raw.githubusercontent.com/shadps4-emu/ps4_cheats/main/PATCHES/Bloodborne.xml",
    };

    public static string FilePath(PatchSource source) => source switch
    {
        PatchSource.GoldHen => Path.Combine(AppPaths.DataRoot, "patches", "GoldHEN", "Bloodborne-Orbis.xml"),
        _ => Path.Combine(AppPaths.DataRoot, "patches", "shadPS4", "Bloodborne.xml"),
    };

    /// <summary>Downloads both patch files (on Update); keeps an old file when that fails. Returns why a download failed, or null.</summary>
    public static async Task<Exception?> UpdateAsync(HttpClient http, CancellationToken cancel = default)
    {
        Exception? failure = null;
        foreach (var source in Downloaded)
        {
            var path = FilePath(source);
            var file = new FileInfo(path);
            try
            {
                var xml = await http.GetStringAsync(SourceUrl(source), cancel);
                GamePatches.Parse(xml, AppVersion); // must be a valid patch file before it replaces the old one
                Directory.CreateDirectory(file.DirectoryName!);
                await File.WriteAllTextAsync(path + ".tmp", xml, cancel);
                File.Move(path + ".tmp", path, overwrite: true);
                lock (Loaded) Loaded.Remove(source);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or System.Xml.XmlException)
            {
                // Offline or the repository moved: the patches already here (if any) stay usable.
                failure = e;
                if (e is HttpRequestException or TaskCanceledException) break; // no connection: the other source fails too
            }
        }
        return failure;
    }

    private static readonly Dictionary<PatchSource, IReadOnlyList<GamePatch>> Loaded = [];

    /// <summary>
    /// The patch behind a catalog entry for <see cref="AppVersion"/>, or null when the file or the patch is missing. Hunters' Accord's
    /// own has no writes here: the mod makes them.
    /// </summary>
    public static GamePatch? Find(PatchEntry entry)
    {
        if (entry.Source == PatchSource.HuntersAccord) return new GamePatch(entry.Name, "Hunters' Accord", "", AppVersion, []);
        lock (Loaded)
        {
            if (!Loaded.TryGetValue(entry.Source, out var patches))
            {
                patches = Load(entry.Source);
                Loaded[entry.Source] = patches;
            }
            return patches.FirstOrDefault(p => p.Name == entry.Name);
        }
    }

    public static void Reload()
    {
        lock (Loaded) Loaded.Clear();
    }

    public static DateTime? UpdatedAt()
    {
        var files = Downloaded.Select(s => new FileInfo(FilePath(s))).ToList();
        return files.All(f => f.Exists) ? files.Min(f => f.LastWriteTime) : null;
    }

    /// <summary>Whether two patches cannot be on together: same bytes, or known not to work together (<see cref="PatchCatalog.Incompatible"/>). The first is applied.</summary>
    public static bool Conflict(PatchEntry a, PatchEntry b) =>
        a.Key != b.Key && (PatchCatalog.AreIncompatible(a, b) || Overlap(a, b));

    private static bool Overlap(PatchEntry a, PatchEntry b) =>
        Find(a) is { } pa && Find(b) is { } pb &&
        pa.Lines.Any(x => pb.Lines.Any(y => x.Rva < y.Rva + y.Bytes.Length && y.Rva < x.Rva + x.Bytes.Length));

    private static IReadOnlyList<GamePatch> Load(PatchSource source)
    {
        try
        {
            var path = FilePath(source);
            return File.Exists(path) ? GamePatches.Parse(File.ReadAllText(path), AppVersion) : [];
        }
        catch (Exception e) when (e is IOException or System.Xml.XmlException)
        {
            return [];
        }
    }
}
