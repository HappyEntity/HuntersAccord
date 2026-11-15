using System.Buffers.Binary;
using System.IO.Compression;

namespace HuntersAccord.Core.Pkg;

/// <summary>
/// The compressed ("PFSC") inner image stored inside the outer PFS. It is split into fixed-size blocks,
/// each either stored raw or zlib-compressed, located through a table of offsets.
/// </summary>
public sealed class PfscImage
{
    public const uint Magic = 0x43534650; // "PFSC"

    private readonly PfsImage _outer;
    private readonly long _base;
    private readonly long[] _blockOffsets;

    private PfscImage(PfsImage outer, long baseOffset, uint blockSize, long length, long[] blockOffsets)
    {
        _outer = outer;
        _base = baseOffset;
        BlockSize = (int)blockSize;
        Length = length;
        _blockOffsets = blockOffsets;
    }

    public int BlockSize { get; }

    /// <summary>Uncompressed size of the inner image.</summary>
    public long Length { get; }

    public int BlockCount => _blockOffsets.Length - 1;

    /// <summary>Finds the PFSC header by scanning block boundaries of the decrypted outer image.</summary>
    public static PfscImage Locate(PfsImage outer)
    {
        using var xts = outer.CreateDecryptor();
        Span<byte> probe = stackalloc byte[4];
        var maxScan = Math.Min(outer.Length, 64L * 1024 * 1024);
        for (long offset = outer.BlockSize; offset < maxScan; offset += outer.BlockSize)
        {
            outer.Read(offset, probe, xts);
            if (BinaryPrimitives.ReadUInt32LittleEndian(probe) == Magic)
                return Open(outer, offset, xts);
        }
        throw new InvalidDataException("Compressed PFS image not found — the keys are probably wrong.");
    }

    private static PfscImage Open(PfsImage outer, long offset, XtsAes128 xts)
    {
        var header = new byte[0x30];
        outer.Read(offset, header, xts);
        var blockSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x0C));
        var mapOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(0x18));
        var length = (long)BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(0x28));
        if (blockSize == 0 || length <= 0) throw new InvalidDataException("Corrupted PFSC header.");

        var blocks = (int)((length + blockSize - 1) / blockSize);
        var map = new byte[(blocks + 1) * 8];
        outer.Read(offset + mapOffset, map, xts);
        var offsets = new long[blocks + 1];
        for (var i = 0; i <= blocks; i++)
            offsets[i] = (long)BinaryPrimitives.ReadUInt64LittleEndian(map.AsSpan(i * 8));

        return new PfscImage(outer, offset, blockSize, length, offsets);
    }

    public PfsImage Outer => _outer;

    /// <summary>Decompresses block <paramref name="index"/> into <paramref name="destination"/> (BlockSize bytes).</summary>
    public void ReadBlock(int index, Span<byte> destination, XtsAes128 xts, byte[] scratch)
    {
        var start = _blockOffsets[index];
        var size = (int)(_blockOffsets[index + 1] - start);
        var output = destination[..BlockSize];

        if (size == BlockSize)
        {
            _outer.Read(_base + start, output, xts);
            return;
        }
        if (size == 0)
        {
            output.Clear();
            return;
        }

        var compressed = scratch.AsSpan(0, size);
        _outer.Read(_base + start, compressed, xts);

        // Blocks are zlib streams, often with a small window (header 0x48 0x89 rather than the usual 0x78 ..);
        // fall back to raw deflate only if the header is not a valid zlib header.
        using var input = new MemoryStream(scratch, 0, size, writable: false);
        using Stream inflater = IsZlibHeader(compressed)
            ? new ZLibStream(input, CompressionMode.Decompress)
            : new DeflateStream(input, CompressionMode.Decompress);
        var total = 0;
        int n;
        while (total < output.Length && (n = inflater.Read(output[total..])) > 0) total += n;
        if (total < output.Length) output[total..].Clear();
    }

    /// <summary>RFC 1950: method 8 (deflate), window ≤ 32K, and CMF·256+FLG divisible by 31.</summary>
    internal static bool IsZlibHeader(ReadOnlySpan<byte> data) =>
        data.Length >= 2 && (data[0] & 0x0F) == 8 && (data[0] >> 4) <= 7 && ((data[0] << 8) | data[1]) % 31 == 0;
}
