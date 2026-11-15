using System.Buffers.Binary;
using System.Text;
using HuntersAccord.Core.Trophies;

namespace HuntersAccord.Core.Tests;

public sealed class TrophyPackTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ha-trp-" + Guid.NewGuid().ToString("N"));

    public TrophyPackTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    /// <summary>A pack as the game's: the header, the 64-byte entries from 0x60, the data after them.</summary>
    private static byte[] Pack(params (string Name, byte[] Data)[] entries)
    {
        var dataAt = TrophyPack.EntriesAt + entries.Length * 64;
        var total = dataAt + entries.Sum(e => e.Data.Length);
        var bytes = new byte[total];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, TrophyPack.Magic);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 3);
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(8), (ulong)total);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), (uint)entries.Length);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), 64);
        var at = dataAt;
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = bytes.AsSpan(TrophyPack.EntriesAt + i * 64, 64);
            Encoding.ASCII.GetBytes(entries[i].Name).CopyTo(entry);
            BinaryPrimitives.WriteUInt64BigEndian(entry[32..], (ulong)at);
            BinaryPrimitives.WriteUInt64BigEndian(entry[40..], (ulong)entries[i].Data.Length);
            entries[i].Data.CopyTo(bytes, at);
            at += entries[i].Data.Length;
        }
        return bytes;
    }

    private string Write(string relative, byte[] bytes)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void Reads_the_entries_and_their_pngs()
    {
        var path = Write("game/sce_sys/trophy/trophy00.trp",
            Pack(("TROP.ESFM", [9, 9, 9]), ("ICON0.PNG", Png), ("TROP000.PNG", Png), ("TROP014.PNG", [.. Png, 7])));
        var pack = TrophyPack.Open(path)!;
        Assert.Equal(4, pack.Names.Count);
        Assert.Equal([.. Png, 7], pack.ReadPng(TrophyPack.PictureName(14)));
        Assert.Null(pack.ReadPng("TROP.ESFM")); // not a picture
        Assert.Null(pack.Read("TROP039.PNG"));
        Assert.Equal("TROP007.PNG", TrophyPack.PictureName(7));
    }

    [Fact]
    public void Not_a_pack_is_null()
    {
        Assert.Null(TrophyPack.Open(Write("x.trp", [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24])));
        Assert.Null(TrophyPack.Open(Path.Combine(_root, "none.trp")));
        var cut = Pack(("TROP000.PNG", Png));
        Assert.Null(TrophyPack.Open(Write("cut.trp", cut[..0x70])));
    }

    [Fact]
    public void The_update_pack_comes_first()
    {
        Write("CUSA03173/sce_sys/trophy/trophy00.trp", Pack(("TROP000.PNG", Png)));
        var basePath = Path.Combine(_root, "CUSA03173");
        var update = Path.Combine(_root, "CUSA03173-UPDATE");
        Assert.Equal(Path.Combine(basePath, "sce_sys", "trophy", "trophy00.trp"), TrophyPack.Find(basePath, update));
        Write("CUSA03173-UPDATE/sce_sys/trophy/trophy00.trp", Pack(("TROP000.PNG", Png)));
        Assert.Equal(Path.Combine(update, "sce_sys", "trophy", "trophy00.trp"), TrophyPack.Find(basePath, update));
        Assert.Null(TrophyPack.Find(null, null));
    }

    [Fact]
    public void The_notices_files_round_trip()
    {
        var slot = new CoopSlot(212, 101, 431);
        Assert.Equal(slot, CoopSlot.Parse(slot.Write()));
        Assert.Null(CoopSlot.Parse("12 3"));
        var glyphs = new ToastGlyphs(19, new Dictionary<int, ToastGlyph> { ['A'] = new(0, 0, 14, 19, 10, 2), ['Ж'] = new(14, 0, 18, 19, 14, 2) });
        var back = ToastGlyphs.Parse(glyphs.Write())!;
        Assert.Equal(19, back.LineHeight);
        Assert.Equal(glyphs.Find('Ж'), back.Find('Ж'));
        Assert.Contains((int)'ё', ToastGlyphs.Set());
        Assert.Equal(64, TrophyToastFiles.Margin(2160));
        Assert.EndsWith("trophy-toast-0-1440-coop-glow.png", TrophyToastFiles.Picture("d", 0, 1440, coop: true, glow: true));
    }

    [Fact]
    public void Pictures_are_taken_out_once_into_the_data_folder()
    {
        var pack = Write("game/sce_sys/trophy/trophy00.trp", Pack(("TROP003.PNG", Png), ("TROP_00.ESFM", [1]), ("GR001.PNG", Png)));
        var data = Path.Combine(_root, "data");
        Assert.NotNull(TrophyPictures.Ensure(data, pack));
        Assert.Equal(Png, File.ReadAllBytes(TrophyPictures.Find(data, 3)!));
        Assert.Null(TrophyPictures.Find(data, 4));
        Assert.False(File.Exists(Path.Combine(TrophyPictures.FolderIn(data), "GR001.PNG"))); // only the trophies' and the icon
        Assert.Empty(Directory.EnumerateFiles(TrophyPictures.FolderIn(data), "*.ESFM"));

        // Stamped: the same pack is not read again (a picture removed by hand stays removed).
        File.Delete(TrophyPictures.FileIn(data, 3));
        TrophyPictures.Ensure(data, pack);
        Assert.Null(TrophyPictures.Find(data, 3));
        Assert.Null(TrophyPictures.Ensure(data, null));
    }
}
