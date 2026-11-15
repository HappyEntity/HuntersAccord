using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace HuntersAccord.Core.Pkg;

/// <summary>
/// AES-128-XTS sector decryption (IEEE P1619) with the sector index as tweak.
/// Not thread-safe: create one instance per worker thread.
/// </summary>
public sealed class XtsAes128 : IDisposable
{
    public const int SectorSize = 0x1000;

    private readonly Aes _data;
    private readonly Aes _tweak;
    private readonly byte[] _tweaks = new byte[SectorSize];

    public XtsAes128(ReadOnlySpan<byte> dataKey, ReadOnlySpan<byte> tweakKey)
    {
        _data = Aes.Create();
        _data.Key = dataKey.ToArray();
        _tweak = Aes.Create();
        _tweak.Key = tweakKey.ToArray();
    }

    /// <summary>Decrypts one or more whole sectors in place; <paramref name="firstSector"/> is the index of the first one.</summary>
    public void DecryptSectors(Span<byte> sectors, ulong firstSector)
    {
        if (sectors.Length % SectorSize != 0) throw new ArgumentException("Length must be a multiple of the sector size.");
        for (var i = 0; i < sectors.Length / SectorSize; i++)
            DecryptSector(sectors.Slice(i * SectorSize, SectorSize), firstSector + (ulong)i);
    }

    private void DecryptSector(Span<byte> sector, ulong index)
    {
        // T0 = E_k2(index), then T(j+1) = T(j) * x in GF(2^128). Precompute all 256 tweaks of the sector,
        // so the data can be decrypted with one bulk ECB call (AES-NI) instead of 256 small ones.
        Span<byte> t = stackalloc byte[16];
        t.Clear();
        BinaryPrimitives.WriteUInt64LittleEndian(t, index);
        _tweak.EncryptEcb(t, t, PaddingMode.None);

        var lo = BinaryPrimitives.ReadUInt64LittleEndian(t);
        var hi = BinaryPrimitives.ReadUInt64LittleEndian(t[8..]);
        var tweaks = MemoryMarshal.Cast<byte, ulong>(_tweaks.AsSpan());
        for (var j = 0; j < SectorSize / 16; j++)
        {
            tweaks[2 * j] = lo;
            tweaks[2 * j + 1] = hi;
            var carry = hi >> 63;
            hi = (hi << 1) | (lo >> 63);
            lo = (lo << 1) ^ (carry * 0x87);
        }

        var data = MemoryMarshal.Cast<byte, ulong>(sector);
        for (var k = 0; k < data.Length; k++) data[k] ^= tweaks[k];
        _data.DecryptEcb(sector, sector, PaddingMode.None);
        for (var k = 0; k < data.Length; k++) data[k] ^= tweaks[k];
    }

    public void Dispose()
    {
        _data.Dispose();
        _tweak.Dispose();
    }
}
