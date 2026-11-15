using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using HuntersAccord.Core.Pkg;
using HuntersAccord.Core.Trophies;

namespace HuntersAccord.Core.Tests;

/// <summary>
/// The trophy key: trial decryption, the game's texts, the key file merge. Everything here is synthetic: a made-up key and
/// NP id encrypt made-up lists the way the game's are (no real key, id or text).
/// </summary>
public sealed class TrophyKeyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ha-trophykey-" + Guid.NewGuid().ToString("N"));

    public TrophyKeyTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static readonly byte[] Key = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");
    private static readonly byte[] OtherKey = Convert.FromHexString("FFEEDDCCBBAA99887766554433221100");
    private const string CommId = "NPWR00000_01";
    private const string Header = "<!--Sce-Np-Trophy-Signature: 0000-->\n<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";

    private static string Config() => Header +
        "<trophyconf version=\"1.1\" platform=\"ps4\"><npcommid>" + CommId + "</npcommid>" +
        "<trophy id=\"000\" hidden=\"no\" ttype=\"P\" pid=\"-1\"/>" +
        "<trophy id=\"001\" hidden=\"yes\" ttype=\"G\" pid=\"000\"/>" +
        "<trophy id=\"002\" hidden=\"no\" ttype=\"B\" pid=\"000\"/></trophyconf>";

    private static string List(string tag) => Header +
        "<trophyconf version=\"1.1\" platform=\"ps4\"><title-name>T</title-name>" +
        $"<trophy id=\"000\" hidden=\"no\" ttype=\"P\" pid=\"-1\"><name>All {tag}</name><detail>Everything {tag}</detail></trophy>" +
        $"<trophy id=\"001\" hidden=\"no\" ttype=\"B\" pid=\"000\"><name>First {tag}</name><detail>One &amp; two {tag}</detail></trophy>" +
        $"<trophy id=\"002\" hidden=\"no\" ttype=\"B\" pid=\"000\"><name>Second {tag}</name><detail>Ж {tag}</detail></trophy></trophyconf>";

    private static byte[] Encrypted(string xml, byte[] key)
    {
        var iv = new byte[16];
        RandomNumberGenerator.Fill(iv);
        return TrophyKey.Encrypt(Encoding.UTF8.GetBytes(xml), TrophyKey.DeriveListKey(key, CommId), iv);
    }

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

    /// <summary>A game copy with a trophy pack under <paramref name="key"/>: the configuration, the default list, US English and Russian.</summary>
    private TrophyPack GamePack(byte[] key, bool russian = true)
    {
        var entries = new List<(string, byte[])>
        {
            ("TROPCONF.ESFM", Encrypted(Config(), key)),
            ("TROP.ESFM", Encrypted(List("default"), key)),
            ("TROP_01.ESFM", Encrypted(List("en"), key)),
            ("TROP000.PNG", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        };
        if (russian) entries.Add(("TROP_08.ESFM", Encrypted(List("ru"), key)));
        var path = Path.Combine(_root, "game", "sce_sys", "trophy", "trophy00.trp");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Pack([.. entries]));
        return TrophyPack.Open(path)!;
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)12)]
    public void A_list_reads_whatever_the_padding_after_it(byte pad)
    {
        // The game's lists end in the cipher's padding: zeros in some languages, PKCS#7 bytes in others (Russian).
        var xml = Encoding.UTF8.GetBytes(List("ru")).Concat(Enumerable.Repeat(pad, 12)).ToArray();
        var texts = OfficialTrophies.Parse(Encoding.UTF8.GetString(xml));
        Assert.Equal(3, texts.Count);
        Assert.Equal("First ru", texts[1].Name);
    }

    [Fact]
    public void The_list_key_is_the_trophy_key_encrypting_the_padded_id()
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        var block = new byte[16];
        Encoding.ASCII.GetBytes(CommId).CopyTo(block, 0);
        Assert.Equal(aes.EncryptEcb(block, PaddingMode.None), TrophyKey.DeriveListKey(Key, CommId)); // one block, zero IV
        Assert.NotEqual(TrophyKey.DeriveListKey(Key, CommId), TrophyKey.DeriveListKey(Key, "NPWR00000_02"));
    }

    [Fact]
    public void A_list_decrypts_with_its_key_only()
    {
        var entry = Encrypted(List("en"), Key);
        Assert.Equal(16 + 16 * ((Encoding.UTF8.GetByteCount(List("en")) + 15) / 16), entry.Length);
        var plain = TrophyKey.Decrypt(entry, TrophyKey.DeriveListKey(Key, CommId))!;
        Assert.Equal(List("en"), Encoding.UTF8.GetString(plain)); // the zero fill dropped
        Assert.True(TrophyKey.LooksLikeList(plain));
        Assert.False(TrophyKey.LooksLikeList(TrophyKey.Decrypt(entry, TrophyKey.DeriveListKey(OtherKey, CommId))!));
        Assert.Null(TrophyKey.Decrypt(entry[..^3], TrophyKey.DeriveListKey(Key, CommId))); // not whole blocks
    }

    [Fact]
    public void A_key_is_valid_when_the_configuration_decrypts()
    {
        var pack = GamePack(Key);
        Assert.Equal(TrophyKeyCheck.Valid, TrophyKey.Check(Key, pack, CommId));
        Assert.Equal(TrophyKeyCheck.Wrong, TrophyKey.Check(OtherKey, pack, CommId));
        Assert.Equal(TrophyKeyCheck.Wrong, TrophyKey.Check(Key, pack, "NPWR99999_00"));
        Assert.Equal(TrophyKeyCheck.NoGame, TrophyKey.Check(Key, null, CommId));
        Assert.Equal(TrophyKeyCheck.NoGame, TrophyKey.Check(Key, pack, null));
    }

    [Theory]
    [InlineData("00112233445566778899aabbccddeeff", true)]
    [InlineData(" 00112233 44556677 8899AABB CCDDEEFF ", true)]
    [InlineData("00112233445566778899AABBCCDDEE", false)]
    [InlineData("00112233445566778899AABBCCDDEEFG", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void A_key_is_32_hex_characters(string? text, bool ok) => Assert.Equal(ok, TrophyKey.Parse(text) is not null);

    [Fact]
    public void The_game_texts_come_in_the_game_language_with_the_configuration_flags()
    {
        var pack = GamePack(Key);
        var ru = OfficialTrophies.Read(pack, Key, CommId, 8)!;
        Assert.Equal("First ru", ru[1].Name);
        Assert.Equal("One & two ru", ru[1].Detail);
        Assert.Equal("Ж ru", ru[2].Detail);
        // Grade and hidden from TROPCONF, not from the language's list.
        Assert.Equal(TrophyGrade.Gold, ru[1].Grade);
        Assert.True(ru[1].Hidden);
        Assert.Equal(TrophyGrade.Platinum, ru[0].Grade);

        Assert.Equal("First en", OfficialTrophies.Read(pack, Key, CommId, 1)![1].Name);
        Assert.Equal("First default", OfficialTrophies.Read(pack, Key, CommId, 4)![1].Name); // no TROP_04: the default list
        Assert.Equal("First default", OfficialTrophies.Read(pack, Key, CommId, 42)![1].Name);
        Assert.Null(OfficialTrophies.Read(pack, OtherKey, CommId, 8));
    }

    [Theory]
    [InlineData(0, "TROP_00.ESFM")]
    [InlineData(1, "TROP_01.ESFM")]
    [InlineData(8, "TROP_08.ESFM")]
    [InlineData(18, "TROP_18.ESFM")]
    [InlineData(20, "TROP_20.ESFM")]
    [InlineData(21, "TROP.ESFM")]
    [InlineData(-1, "TROP.ESFM")]
    public void A_ps4_language_has_its_list(int language, string name) => Assert.Equal(name, OfficialTrophies.ListName(language));

    [Fact]
    public void The_game_words_replace_ours_in_every_language()
    {
        var info = TrophyCatalog.Find(1)!;
        var applied = OfficialTrophies.Apply(info, new Dictionary<int, OfficialTrophy> { [1] = new(1, "Game name", "Game detail", TrophyGrade.Silver, false) });
        Assert.Equal("Game name", applied.Name("ru"));
        Assert.Equal("Game name", applied.Name("en"));
        Assert.Equal("Game detail", applied.Description("ru"));
        Assert.Equal(TrophyGrade.Silver, applied.Grade);
        Assert.False(applied.Hidden);
        Assert.Same(info, OfficialTrophies.Apply(info, null));
        Assert.Same(info, OfficialTrophies.Apply(info, new Dictionary<int, OfficialTrophy>()));
    }

    [Fact]
    public void The_id_comes_from_npbind_else_the_known_one()
    {
        var game = Path.Combine(_root, "CUSA03173");
        Assert.Equal("NPWR05818_00", TrophyKey.CommIdOf(game, null, "CUSA03173"));
        Assert.Null(TrophyKey.CommIdOf(game, null, "CUSA00000"));
        Directory.CreateDirectory(Path.Combine(game, "sce_sys"));
        File.WriteAllBytes(Path.Combine(game, "sce_sys", "npbind.dat"), [0xD2, 0x94, .. Encoding.ASCII.GetBytes("xxNPWR12345_07"), 0, 0]);
        Assert.Equal("NPWR12345_07", TrophyKey.CommIdOf(game, null, "CUSA00000"));
    }

    [Fact]
    public void The_key_files_first_fitting_key_is_used()
    {
        var good = TrophyKey.Format(Key);
        var bad = TrophyKey.Format(OtherKey);
        TrophyKeyCheck Check(byte[] k) => k.SequenceEqual(Key) ? TrophyKeyCheck.Valid : TrophyKeyCheck.Wrong;

        var choice = TrophyKey.Choose([bad, good], Check); // ours wrong, shadPS4's fits
        Assert.Equal(TrophyKeyState.Valid, choice.State);
        Assert.Equal(Key, choice.Key);
        Assert.Equal(TrophyKeyState.Wrong, TrophyKey.Choose([bad, null, "zz"], Check).State);
        Assert.Null(TrophyKey.Choose([bad], Check).Key);
        Assert.Equal(TrophyKeyState.None, TrophyKey.Choose([null, " "], Check).State);
        Assert.Equal(TrophyKeyState.Unverified, TrophyKey.Choose([bad], _ => TrophyKeyCheck.NoGame).State);
        Assert.DoesNotContain(good, TrophyKey.Choose([good], Check).ToString()); // the key never goes into a string
    }

    // ---- The key file -----------------------------------------------------------------------------------------------

    private static readonly string[] Fields = ["Modulus", "PublicExponent", "PrivateExponent", "Prime1", "Prime2", "Exponent1", "Exponent2", "Coefficient"];

    private static JsonObject Keyset(string fill) => new(Fields.Select(f => KeyValuePair.Create(f, (JsonNode?)JsonValue.Create(fill))));

    private string KeyJson(string name, JsonObject content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content.ToJsonString());
        return path;
    }

    private static JsonObject Read(string path) => (JsonObject)JsonNode.Parse(File.ReadAllText(path))!;

    private static string? TrophyOf(string path) => KeyFile.ReadTrophyKey(path);

    [Fact]
    public void A_wrong_key_never_replaces_a_stored_one()
    {
        Assert.Equal(ImportedTrophyKey.Valid, KeyFile.Decide(hasStored: true, TrophyKeyCheck.Valid));
        Assert.Equal(ImportedTrophyKey.Wrong, KeyFile.Decide(hasStored: true, TrophyKeyCheck.Wrong));
        Assert.Equal(ImportedTrophyKey.Wrong, KeyFile.Decide(hasStored: false, TrophyKeyCheck.Wrong));
        Assert.Equal(ImportedTrophyKey.KeptStored, KeyFile.Decide(hasStored: true, TrophyKeyCheck.NoGame));
        Assert.Equal(ImportedTrophyKey.Unverified, KeyFile.Decide(hasStored: false, TrophyKeyCheck.NoGame));

        var own = KeyJson("own.json", new JsonObject { ["FakeKeyset"] = Keyset("AB"), ["TrophyKeySet"] = new JsonObject { ["ReleaseTrophyKey"] = TrophyKey.Format(Key) } });
        Assert.Equal(ImportedTrophyKey.Wrong, KeyFile.StoreTrophyKey(own, OtherKey, TrophyKeyCheck.Wrong));
        Assert.Equal(TrophyKey.Format(Key), TrophyOf(own));
        Assert.Equal(ImportedTrophyKey.KeptStored, KeyFile.StoreTrophyKey(own, OtherKey, TrophyKeyCheck.NoGame));
        Assert.Equal(TrophyKey.Format(Key), TrophyOf(own));
        Assert.Equal(ImportedTrophyKey.Valid, KeyFile.StoreTrophyKey(own, OtherKey, TrophyKeyCheck.Valid));
        Assert.Equal(TrophyKey.Format(OtherKey), TrophyOf(own));
        Assert.Equal("AB", (string?)Read(own)["FakeKeyset"]!["Modulus"]); // the rest kept
    }

    [Fact]
    public void Import_merges_and_keeps_the_good_sections()
    {
        var target = KeyJson("own.json", new JsonObject
        {
            ["FakeKeyset"] = Keyset("01"), ["PkgDerivedKey3Keyset"] = Keyset("02"), ["DebugRifKeyset"] = Keyset("03"),
            ["TrophyKeySet"] = new JsonObject { ["ReleaseTrophyKey"] = TrophyKey.Format(Key) }, ["Other"] = 5,
        });
        TrophyKeyCheck Check(byte[] k) => k.SequenceEqual(Key) ? TrophyKeyCheck.Valid : TrophyKeyCheck.Wrong;

        // The release template: empty PKG keys, an empty trophy key: nothing to import, nothing changed.
        var empty = KeyJson("empty.json", new JsonObject { ["FakeKeyset"] = Keyset(""), ["PkgDerivedKey3Keyset"] = Keyset(""), ["TrophyKeySet"] = new JsonObject { ["ReleaseTrophyKey"] = "" } });
        Assert.Throws<InvalidDataException>(() => KeyFile.Import(empty, target, Check));
        Assert.Equal("01", (string?)Read(target)["FakeKeyset"]!["Modulus"]);

        // Complete PKG keys with a wrong trophy key: the keys taken, the stored trophy key kept.
        var pkgOnly = KeyJson("pkg.json", new JsonObject { ["FakeKeyset"] = Keyset("AA"), ["PkgDerivedKey3Keyset"] = Keyset("BB"), ["TrophyKeySet"] = new JsonObject { ["ReleaseTrophyKey"] = TrophyKey.Format(OtherKey) } });
        var result = KeyFile.Import(pkgOnly, target, Check);
        Assert.True(result.PkgKeys);
        Assert.Equal(ImportedTrophyKey.Wrong, result.TrophyKey);
        var merged = Read(target);
        Assert.Equal("AA", (string?)merged["FakeKeyset"]!["Modulus"]);
        Assert.Equal("BB", (string?)merged["PkgDerivedKey3Keyset"]!["Modulus"]);
        Assert.Equal("03", (string?)merged["DebugRifKeyset"]!["Modulus"]); // not in the file: kept
        Assert.Equal(5, (int)merged["Other"]!);
        Assert.Equal(TrophyKey.Format(Key), TrophyOf(target));

        // Half PKG keys: not taken (the complete ones stay).
        var half = Keyset("CC");
        half["Prime2"] = "";
        var partial = KeyJson("partial.json", new JsonObject { ["FakeKeyset"] = half, ["PkgDerivedKey3Keyset"] = Keyset("DD"), ["TrophyKeySet"] = new JsonObject { ["ReleaseTrophyKey"] = TrophyKey.Format(Key) } });
        result = KeyFile.Import(partial, target, Check);
        Assert.False(result.PkgKeys);
        Assert.Equal(ImportedTrophyKey.Valid, result.TrophyKey);
        Assert.Equal("AA", (string?)Read(target)["FakeKeyset"]!["Modulus"]);
    }

    [Fact]
    public void A_file_with_only_a_trophy_key_imports()
    {
        var target = Path.Combine(_root, "data", "keys.json"); // no file yet
        var source = KeyJson("trophy.json", new JsonObject { ["TrophyKeySet"] = new JsonObject { ["ReleaseTrophyKey"] = TrophyKey.Format(Key).ToLowerInvariant() } });
        var result = KeyFile.Import(source, target, _ => TrophyKeyCheck.Valid);
        Assert.Equal(new KeyImport(false, ImportedTrophyKey.Valid), result);
        Assert.Equal(TrophyKey.Format(Key), TrophyOf(target));
        Assert.Null(PkgKeys.TryLoad([target])); // no PKG keys came with it

        // No game copy to check a new one on: a stored key is kept; with none stored, it is stored unverified.
        var other = KeyJson("other.json", new JsonObject { ["TrophyKeySet"] = new JsonObject { ["ReleaseTrophyKey"] = TrophyKey.Format(OtherKey) } });
        Assert.Equal(ImportedTrophyKey.KeptStored, KeyFile.Import(other, target, _ => TrophyKeyCheck.NoGame).TrophyKey);
        Assert.Equal(TrophyKey.Format(Key), TrophyOf(target));
        var fresh = Path.Combine(_root, "fresh", "keys.json");
        Assert.Equal(ImportedTrophyKey.Unverified, KeyFile.Import(other, fresh, _ => TrophyKeyCheck.NoGame).TrophyKey);
        Assert.Equal(TrophyKey.Format(OtherKey), TrophyOf(fresh));

        // Not a key at all: not stored, and the import says so.
        var junk = KeyJson("junk.json", new JsonObject { ["TrophyKeySet"] = new JsonObject { ["ReleaseTrophyKey"] = "not a key" } });
        Assert.Equal(ImportedTrophyKey.Wrong, KeyFile.Import(junk, target, _ => TrophyKeyCheck.Valid).TrophyKey);
        Assert.Equal(TrophyKey.Format(Key), TrophyOf(target));
    }
}
