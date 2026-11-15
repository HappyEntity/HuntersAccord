using System.Buffers.Binary;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace HuntersAccord.Core.Pkg;

/// <summary>One row of the PKG entry table (all fields big-endian on disk).</summary>
public readonly record struct PkgEntry(uint Id, uint NameOffset, uint Flags1, uint Flags2, uint Offset, uint Size, string? Name)
{
    public const int RowSize = 32;

    /// <summary>Entry payload is encrypted with a per-entry key; Hunters' Accord only needs the unencrypted ones.</summary>
    public bool IsEncrypted => (Flags1 & 0x8000_0000) != 0;
}

/// <summary>
/// Read-only view of a PS4 package (".pkg", magic <c>\x7FCNT</c>): header, entry table and PFS image location.
/// Thread-safe: all reads go through <see cref="RandomAccess"/> on a shared handle.
/// </summary>
public sealed class PkgFile : IDisposable
{
    public const uint Magic = 0x7F434E54;

    public const uint EntryIdEntryKeys = 0x0010;
    public const uint EntryIdImageKey = 0x0020;
    public const uint EntryIdEntryNames = 0x0200;
    public const uint EntryIdParamSfo = 0x1000;

    private readonly byte[] _entryTableRaw;

    private PkgFile(string path, SafeFileHandle handle, byte[] header, byte[] entryTableRaw, IReadOnlyList<PkgEntry> entries)
    {
        Path = path;
        Handle = handle;
        _entryTableRaw = entryTableRaw;
        Entries = entries;

        ContentId = Encoding.ASCII.GetString(header, 0x40, 0x24).TrimEnd('\0');
        PfsImageOffset = (long)BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(0x410));
        PfsImageSize = (long)BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(0x418));
    }

    public string Path { get; }
    public SafeFileHandle Handle { get; }
    public string ContentId { get; }
    public long PfsImageOffset { get; }
    public long PfsImageSize { get; }
    public IReadOnlyList<PkgEntry> Entries { get; }

    /// <summary>Title ID embedded in the content ID, e.g. <c>EP9000-CUSA03173_00-...</c> → <c>CUSA03173</c>.</summary>
    public string TitleId => ContentId.Length >= 16 ? ContentId.Substring(7, 9) : "";

    public static PkgFile Open(string path)
    {
        var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
        try
        {
            var header = new byte[0x1000];
            RandomAccess.Read(handle, header, 0);
            if (BinaryPrimitives.ReadUInt32BigEndian(header) != Magic)
                throw new InvalidDataException($"{System.IO.Path.GetFileName(path)} is not a PS4 package.");

            var count = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0x10));
            var tableOffset = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0x18));
            if (count is 0 or > 4096) throw new InvalidDataException("Corrupted PKG entry table.");

            var table = new byte[count * PkgEntry.RowSize];
            RandomAccess.Read(handle, table, tableOffset);

            var rows = new PkgEntry[count];
            for (var i = 0; i < count; i++)
            {
                var r = table.AsSpan(i * PkgEntry.RowSize);
                rows[i] = new PkgEntry(
                    BinaryPrimitives.ReadUInt32BigEndian(r),
                    BinaryPrimitives.ReadUInt32BigEndian(r[4..]),
                    BinaryPrimitives.ReadUInt32BigEndian(r[8..]),
                    BinaryPrimitives.ReadUInt32BigEndian(r[12..]),
                    BinaryPrimitives.ReadUInt32BigEndian(r[16..]),
                    BinaryPrimitives.ReadUInt32BigEndian(r[20..]),
                    null);
            }

            // Resolve names through the entry-name table (entry 0x200).
            var namesRow = Array.Find(rows, e => e.Id == EntryIdEntryNames);
            if (namesRow.Id == EntryIdEntryNames)
            {
                var names = new byte[namesRow.Size];
                RandomAccess.Read(handle, names, namesRow.Offset);
                for (var i = 0; i < rows.Length; i++)
                {
                    if (rows[i].NameOffset == 0 || rows[i].NameOffset >= names.Length) continue;
                    var span = names.AsSpan((int)rows[i].NameOffset);
                    var end = span.IndexOf((byte)0);
                    rows[i] = rows[i] with { Name = Encoding.ASCII.GetString(end < 0 ? span : span[..end]) };
                }
            }

            return new PkgFile(path, handle, header, table, rows);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public PkgEntry? FindEntry(uint id) => Entries.Where(e => e.Id == id).Select(e => (PkgEntry?)e).FirstOrDefault();

    public byte[] ReadEntry(PkgEntry entry)
    {
        var data = new byte[entry.Size];
        RandomAccess.Read(Handle, data, entry.Offset);
        return data;
    }

    public byte[] ReadEntry(uint id) =>
        ReadEntry(FindEntry(id) ?? throw new InvalidDataException($"PKG entry 0x{id:X} is missing."));

    /// <summary>The raw 32-byte table row of an entry, as stored in the file (used in key derivation).</summary>
    public ReadOnlySpan<byte> RawRow(uint id)
    {
        for (var i = 0; i < Entries.Count; i++)
            if (Entries[i].Id == id)
                return _entryTableRaw.AsSpan(i * PkgEntry.RowSize, PkgEntry.RowSize);
        throw new InvalidDataException($"PKG entry 0x{id:X} is missing.");
    }

    public ParamSfo ReadParamSfo() => ParamSfo.Parse(ReadEntry(EntryIdParamSfo));

    public void Dispose() => Handle.Dispose();
}
