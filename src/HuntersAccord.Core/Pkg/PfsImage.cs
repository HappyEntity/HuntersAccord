using System.Buffers;
using System.Buffers.Binary;

namespace HuntersAccord.Core.Pkg;

/// <summary>
/// The package's outer PFS image, decrypted on the fly. Sectors before <see cref="FirstEncryptedSector"/>
/// (the superblock area) are stored in plain text.
/// </summary>
public sealed class PfsImage
{
    public const ulong SuperblockMagic = 0x01332A0B;
    private const int SeedOffset = 0x370;
    private const int SeedSize = 16;

    private readonly PkgFile _pkg;
    private readonly byte[] _dataKey;
    private readonly byte[] _tweakKey;

    public PfsImage(PkgFile pkg, byte[] ekpfs)
    {
        _pkg = pkg;
        var super = new byte[0x400];
        RandomAccess.Read(pkg.Handle, super, pkg.PfsImageOffset);
        if (BinaryPrimitives.ReadUInt64LittleEndian(super.AsSpan(8)) != SuperblockMagic)
            throw new InvalidDataException("PFS superblock not found in package.");

        BlockSize = BinaryPrimitives.ReadUInt32LittleEndian(super.AsSpan(0x20));
        FirstEncryptedSector = (ulong)(BlockSize / XtsAes128.SectorSize);
        (_dataKey, _tweakKey) = PkgCrypto.DerivePfsKeys(ekpfs, super.AsSpan(SeedOffset, SeedSize));
    }

    public uint BlockSize { get; }
    public ulong FirstEncryptedSector { get; }
    public long Length => _pkg.PfsImageSize;

    /// <summary>Creates a decryptor for the calling thread (XTS state is not shareable).</summary>
    public XtsAes128 CreateDecryptor() => new(_dataKey, _tweakKey);

    /// <summary>Reads and decrypts <paramref name="destination"/>.Length bytes at <paramref name="offset"/> in the image.</summary>
    public void Read(long offset, Span<byte> destination, XtsAes128 xts)
    {
        const int sector = XtsAes128.SectorSize;
        var start = offset / sector * sector;
        var end = (offset + destination.Length + sector - 1) / sector * sector;
        var length = (int)(end - start);

        var rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var buffer = rented.AsSpan(0, length);
            var read = RandomAccess.Read(_pkg.Handle, buffer, _pkg.PfsImageOffset + start);
            if (read < length) buffer[read..].Clear();

            var firstSector = (ulong)(start / sector);
            for (var s = 0; s < length / sector; s++)
            {
                var index = firstSector + (ulong)s;
                if (index >= FirstEncryptedSector)
                    xts.DecryptSectors(buffer.Slice(s * sector, sector), index);
            }

            buffer.Slice((int)(offset - start), destination.Length).CopyTo(destination);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
