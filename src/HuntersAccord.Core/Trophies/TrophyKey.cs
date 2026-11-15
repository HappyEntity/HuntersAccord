using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace HuntersAccord.Core.Trophies;

/// <summary>What a trial decryption of a game copy's trophy list said about a trophy key.</summary>
public enum TrophyKeyCheck
{
    /// <summary>The key opens the list (its configuration decrypts to the trophy signature).</summary>
    Valid,

    /// <summary>The key does not open the list.</summary>
    Wrong,

    /// <summary>Nothing to check against: no game copy, no trophy pack, or no configuration in it.</summary>
    NoGame,
}

/// <summary>Which trophy key is in use.</summary>
public enum TrophyKeyState
{
    /// <summary>No key in any key file.</summary>
    None,

    /// <summary>It opens the game copy's trophies.</summary>
    Valid,

    /// <summary>No game copy to check it against yet.</summary>
    Unverified,

    /// <summary>The key files' keys do not open the game copy's trophies: none is used.</summary>
    Wrong,
}

/// <summary>The key chosen (null unless usable), the text it came from (also a rejected one), its state.</summary>
public sealed record TrophyKeyChoice(byte[]? Key, string? Text, TrophyKeyState State)
{
    /// <summary>The state only: the key never goes into a string that could reach a log.</summary>
    public override string ToString() => $"TrophyKeyChoice {{ State = {State} }}";
}

/// <summary>
/// The trophy key (16 bytes, the user's own, never shipped) and the trophy lists' encryption, written from the format's
/// description (no emulator code): the key of a game's lists is the trophy key AES-128-CBC-encrypting the game's NP
/// communication id (ASCII, zero-padded to 16 bytes) with a zero IV; each <c>*.ESFM</c> entry is a 16-byte IV followed by
/// AES-128-CBC ciphertext under that key, without padding. The plaintext is the list's XML, after the trophy signature
/// comment (<see cref="Signature"/>). A key is valid for a copy when its configuration (<c>TROPCONF.ESFM</c>) decrypts.
/// The key is never logged nor written anywhere but the launcher's own keys.json.
/// </summary>
public static partial class TrophyKey
{
    public const int Size = 16;
    public const string ConfigName = "TROPCONF.ESFM";
    public const string Signature = "<!--Sce-Np-Trophy-Signature";

    /// <summary>The key typed or read (32 hex characters, spaces ignored); null when it is not that.</summary>
    public static byte[]? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var hex = string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        if (hex.Length != Size * 2 || !hex.All(Uri.IsHexDigit)) return null;
        return Convert.FromHexString(hex);
    }

    /// <summary>The key as it is stored (upper-case hex).</summary>
    public static string Format(ReadOnlySpan<byte> key) => Convert.ToHexString(key);

    /// <summary>The lists' key of a game: the trophy key encrypting its NP communication id.</summary>
    public static byte[] DeriveListKey(ReadOnlySpan<byte> trophyKey, string npCommId)
    {
        if (trophyKey.Length != Size) throw new ArgumentException("A trophy key is 16 bytes.", nameof(trophyKey));
        var block = new byte[Size];
        var id = Encoding.ASCII.GetBytes(npCommId);
        id.AsSpan(0, Math.Min(id.Length, Size)).CopyTo(block);
        using var aes = Aes.Create();
        aes.Key = trophyKey.ToArray();
        return aes.EncryptCbc(block, new byte[Size], PaddingMode.None);
    }

    /// <summary>A list entry decrypted (trailing zero bytes dropped); null when it is not a whole number of blocks.</summary>
    public static byte[]? Decrypt(ReadOnlySpan<byte> entry, ReadOnlySpan<byte> listKey)
    {
        if (entry.Length < 2 * Size || entry.Length % Size != 0) return null;
        using var aes = Aes.Create();
        aes.Key = listKey.ToArray();
        var plain = aes.DecryptCbc(entry[Size..], entry[..Size], PaddingMode.None);
        var end = plain.Length;
        while (end > 0 && plain[end - 1] == 0) end--;
        return end == plain.Length ? plain : plain[..end];
    }

    /// <summary>A list's plaintext in the format (the signature or an XML declaration first).</summary>
    public static bool LooksLikeList(ReadOnlySpan<byte> plain) =>
        plain.StartsWith(Encoding.ASCII.GetBytes(Signature)) || plain.StartsWith("<?xml"u8);

    /// <summary>Encrypts a list the way the game's are (the tests' synthetic packs; nothing real is ever made here).</summary>
    public static byte[] Encrypt(ReadOnlySpan<byte> plain, ReadOnlySpan<byte> listKey, ReadOnlySpan<byte> iv)
    {
        var padded = new byte[(plain.Length + Size - 1) / Size * Size];
        plain.CopyTo(padded);
        using var aes = Aes.Create();
        aes.Key = listKey.ToArray();
        return [.. iv, .. aes.EncryptCbc(padded, iv, PaddingMode.None)];
    }

    /// <summary>Tries <paramref name="key"/> on the configuration of <paramref name="pack"/> (null pack: nothing to check).</summary>
    public static TrophyKeyCheck Check(ReadOnlySpan<byte> key, TrophyPack? pack, string? npCommId)
    {
        if (pack is null || npCommId is null || pack.Read(ConfigName) is not { } config) return TrophyKeyCheck.NoGame;
        if (key.Length != Size) return TrophyKeyCheck.Wrong;
        return Decrypt(config, DeriveListKey(key, npCommId)) is { } plain && LooksLikeList(plain)
            ? TrophyKeyCheck.Valid : TrophyKeyCheck.Wrong;
    }

    /// <summary>
    /// A game copy's NP communication id: from its <c>sce_sys/npbind.dat</c> (the update's first), else the one known for its
    /// title id (dumps often come without the file); null when neither.
    /// </summary>
    public static string? CommIdOf(string? basePath, string? updatePath, string? titleId)
    {
        foreach (var folder in new[] { updatePath, basePath })
        {
            if (string.IsNullOrEmpty(folder)) continue;
            var file = Path.Combine(folder, "sce_sys", "npbind.dat");
            try
            {
                if (!File.Exists(file) || new FileInfo(file).Length > 1024 * 1024) continue;
                var match = CommIdPattern().Match(Encoding.ASCII.GetString(File.ReadAllBytes(file)));
                if (match.Success) return match.Value;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // The known id below.
            }
        }
        return titleId is not null && KnownCommIds.TryGetValue(titleId, out var known) ? known : null;
    }

    /// <summary>NP communication ids of supported titles (public: the PlayStation Network's trophy list ids).</summary>
    public static IReadOnlyDictionary<string, string> KnownCommIds { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["CUSA03173"] = "NPWR05818_00", // Bloodborne Game of the Year Edition (EU)
    };

    /// <summary>
    /// The key to use among key files' texts in order (the launcher's own keys.json, then shadPS4's): the first that opens
    /// the game copy; with no copy to check against, the first well-formed one (unverified); none that fits: no key, the
    /// first text kept to show what was rejected.
    /// </summary>
    public static TrophyKeyChoice Choose(IEnumerable<string?> texts, Func<byte[], TrophyKeyCheck> check)
    {
        string? rejected = null;
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            rejected ??= text;
            if (Parse(text) is not { } key) continue;
            switch (check(key))
            {
                case TrophyKeyCheck.Valid: return new TrophyKeyChoice(key, text, TrophyKeyState.Valid);
                case TrophyKeyCheck.NoGame: return new TrophyKeyChoice(key, text, TrophyKeyState.Unverified);
            }
        }
        return rejected is null ? new TrophyKeyChoice(null, null, TrophyKeyState.None) : new TrophyKeyChoice(null, rejected, TrophyKeyState.Wrong);
    }

    [GeneratedRegex(@"NPWR\d{5}_\d{2}")]
    private static partial Regex CommIdPattern();
}
