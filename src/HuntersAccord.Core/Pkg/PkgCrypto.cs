using System.Buffers.Binary;
using System.Security.Cryptography;

namespace HuntersAccord.Core.Pkg;

/// <summary>Derives the PFS image keys of a fake-signed package from the user-provided RSA keysets.</summary>
public static class PkgCrypto
{
    /// <summary>Entry 0x10 holds a 0x20 seed digest, 7 digests of 0x20 and then 7 RSA-encrypted keys of 0x100.</summary>
    private const int EntryKeysKeyTableOffset = 0x100;
    private const int EntryKeySize = 0x100;
    private const int DerivedKey3Index = 3;

    /// <summary>Recovers EKPFS, the root key of the package's PFS image.</summary>
    public static byte[] DeriveEkpfs(PkgFile pkg, PkgKeys keys)
    {
        // 1. Derived key 3 = RSA-decrypt(entry_keys.key[3]).
        var entryKeys = pkg.ReadEntry(PkgFile.EntryIdEntryKeys);
        var encryptedDk3 = entryKeys.AsSpan(EntryKeysKeyTableOffset + DerivedKey3Index * EntryKeySize, EntryKeySize);
        byte[] dk3;
        using (var rsa = keys.PkgDerivedKey3Keyset.CreateRsa())
            dk3 = RsaDecrypt(rsa, encryptedDk3, "PkgDerivedKey3Keyset");

        // 2. IV/key for the image key = SHA-256(image-key table row || dk3); first half IV, second half key.
        Span<byte> material = stackalloc byte[PkgEntry.RowSize + 32];
        pkg.RawRow(PkgFile.EntryIdImageKey).CopyTo(material);
        dk3.AsSpan(0, Math.Min(32, dk3.Length)).CopyTo(material[PkgEntry.RowSize..]);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(material, hash);

        byte[] imageKey;
        using (var aes = Aes.Create())
        {
            aes.Key = hash[16..].ToArray();
            imageKey = aes.DecryptCbc(pkg.ReadEntry(PkgFile.EntryIdImageKey), hash[..16], PaddingMode.None);
        }

        // 3. EKPFS = RSA-decrypt(image key).
        using var fake = keys.FakeKeyset.CreateRsa();
        return RsaDecrypt(fake, imageKey, "FakeKeyset");
    }

    /// <summary>
    /// XTS keys of the PFS image: HMAC-SHA256(EKPFS, LE32(1) || seed); first half is the tweak key, second the data key.
    /// </summary>
    public static (byte[] DataKey, byte[] TweakKey) DerivePfsKeys(ReadOnlySpan<byte> ekpfs, ReadOnlySpan<byte> seed)
    {
        Span<byte> input = stackalloc byte[4 + seed.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(input, 1);
        seed.CopyTo(input[4..]);
        var mac = HMACSHA256.HashData(ekpfs, input);
        return (mac[16..], mac[..16]);
    }

    private static byte[] RsaDecrypt(RSA rsa, ReadOnlySpan<byte> data, string keysetName)
    {
        try
        {
            return rsa.Decrypt(data.ToArray(), RSAEncryptionPadding.Pkcs1);
        }
        catch (CryptographicException e)
        {
            throw new InvalidDataException($"{keysetName} does not decrypt this package (wrong key?).", e);
        }
    }
}
