using System.Security.Cryptography;
using System.Text.Json;

namespace HuntersAccord.Core.Pkg;

/// <summary>An RSA-2048 private key given as big-endian hex fields (the format shadPS4's key manager stores).</summary>
public sealed record RsaKeyset(
    byte[] Modulus, byte[] PublicExponent, byte[] PrivateExponent,
    byte[] Prime1, byte[] Prime2, byte[] Exponent1, byte[] Exponent2, byte[] Coefficient)
{
    public bool IsComplete =>
        Modulus.Length > 0 && PublicExponent.Length > 0 && PrivateExponent.Length > 0 &&
        Prime1.Length > 0 && Prime2.Length > 0 && Exponent1.Length > 0 && Exponent2.Length > 0 && Coefficient.Length > 0;

    public RSA CreateRsa()
    {
        var n = Modulus.Length;
        var half = (n + 1) / 2;
        var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = Modulus,
            Exponent = PublicExponent,
            D = Pad(PrivateExponent, n),
            P = Pad(Prime1, half),
            Q = Pad(Prime2, half),
            DP = Pad(Exponent1, half),
            DQ = Pad(Exponent2, half),
            InverseQ = Pad(Coefficient, half),
        });
        return rsa;
    }

    // .NET requires fixed-size big-endian parameters: left-pad with zeros or drop superfluous leading zeros.
    private static byte[] Pad(byte[] value, int size)
    {
        var trimmed = value.AsSpan().TrimStart((byte)0);
        if (trimmed.Length > size) throw new CryptographicException("RSA key field is too long.");
        var result = new byte[size];
        trimmed.CopyTo(result.AsSpan(size - trimmed.Length));
        return result;
    }
}

/// <summary>
/// Keys needed to unpack a package. Hunters' Accord does not ship any keys: they are provided by the user,
/// in the same <c>keys.json</c> format shadPS4 uses (so keys entered in shadPS4 are picked up automatically). A chosen
/// file is merged into the launcher's own, never copied over it (<see cref="KeyFile.Import"/>).
/// </summary>
public sealed record PkgKeys(RsaKeyset FakeKeyset, RsaKeyset PkgDerivedKey3Keyset)
{
    public const string FileName = "keys.json";

    public bool IsComplete => FakeKeyset.IsComplete && PkgDerivedKey3Keyset.IsComplete;

    /// <summary>Where Hunters' Accord looks for keys: its own data folder first, then shadPS4's key manager file.</summary>
    public static IReadOnlyList<string> DefaultLocations(string shadPs4UserDir) =>
    [
        Path.Combine(AppPaths.DataRoot, FileName),
        Path.Combine(shadPs4UserDir, FileName),
    ];

    /// <summary>Checks that the keys actually unlock <paramref name="pkgPath"/> (cheap: header entries only).</summary>
    public bool CanDecrypt(string pkgPath)
    {
        try
        {
            using var pkg = PkgFile.Open(pkgPath);
            PkgCrypto.DeriveEkpfs(pkg, this);
            return true;
        }
        catch (Exception e) when (e is InvalidDataException or CryptographicException)
        {
            return false;
        }
    }

    /// <summary>Returns the first complete key file among <paramref name="candidates"/>, or null.</summary>
    public static PkgKeys? TryLoad(IEnumerable<string> candidates)
    {
        foreach (var path in candidates)
        {
            try
            {
                if (!File.Exists(path)) continue;
                var keys = Parse(File.ReadAllText(path));
                if (keys.IsComplete) return keys;
            }
            catch (Exception e) when (e is JsonException or FormatException or IOException)
            {
                // Try the next candidate.
            }
        }
        return null;
    }

    public static PkgKeys Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return new PkgKeys(
            ReadKeyset(doc.RootElement, "FakeKeyset"),
            ReadKeyset(doc.RootElement, "PkgDerivedKey3Keyset"));
    }

    private static RsaKeyset ReadKeyset(JsonElement root, string name)
    {
        root.TryGetProperty(name, out var ks);
        byte[] Field(string field) =>
            ks.ValueKind == JsonValueKind.Object && ks.TryGetProperty(field, out var v) && v.GetString() is { Length: > 0 } hex
                ? Convert.FromHexString(string.Concat(hex.Where(c => !char.IsWhiteSpace(c))))
                : [];

        return new RsaKeyset(
            Field("Modulus"), Field("PublicExponent"), Field("PrivateExponent"),
            Field("Prime1"), Field("Prime2"), Field("Exponent1"), Field("Exponent2"), Field("Coefficient"));
    }
}
