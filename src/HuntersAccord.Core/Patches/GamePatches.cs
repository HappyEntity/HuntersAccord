using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace HuntersAccord.Core.Patches;

public sealed record PatchLine(long Rva, byte[] Bytes);

public sealed record GamePatch(string Name, string Author, string Note, string AppVersion, IReadOnlyList<PatchLine> Lines);

/// <summary>
/// Reads patch files in the shadPS4 / GoldHEN XML format:
/// <c>&lt;Patch&gt;&lt;Metadata Name Author Note AppVer AppElf&gt;&lt;PatchList&gt;&lt;Line Type Address Value/&gt;</c>.
/// Addresses are eboot addresses with the PS4 load base (<see cref="LoadBase"/>); Hunters' Accord stores offsets into the eboot.
/// Patches with lines Hunters' Accord cannot apply as fixed writes (pattern masks) are left out.
/// </summary>
public static class GamePatches
{
    public const long LoadBase = 0x400000;

    public static IReadOnlyList<GamePatch> Parse(string xml, string appVersion, string elf = "eboot.bin")
    {
        var result = new List<GamePatch>();
        foreach (var meta in XDocument.Parse(xml).Descendants("Metadata"))
        {
            if ((string?)meta.Attribute("AppVer") != appVersion) continue;
            var appElf = (string?)meta.Attribute("AppElf");
            if (appElf is not null && !appElf.Equals(elf, StringComparison.OrdinalIgnoreCase)) continue;

            var lines = new List<PatchLine>();
            var supported = true;
            foreach (var line in meta.Descendants("Line"))
            {
                if (ParseLine((string?)line.Attribute("Type"), (string?)line.Attribute("Address"), (string?)line.Attribute("Value")) is { } parsed)
                    lines.Add(parsed);
                else
                    supported = false;
            }
            if (!supported || lines.Count == 0) continue;
            result.Add(new GamePatch(
                (string?)meta.Attribute("Name") ?? "",
                (string?)meta.Attribute("Author") ?? "",
                (string?)meta.Attribute("Note") ?? "",
                appVersion,
                lines));
        }
        return result;
    }

    private static PatchLine? ParseLine(string? type, string? address, string? value)
    {
        if (type is null || address is null || value is null) return null;
        if (!TryHex(address, out var addr) || addr < LoadBase) return null;
        var rva = addr - LoadBase;
        byte[]? bytes = type switch
        {
            "bytes" => FromHexString(value),
            "byte" => TryHex(value, out var b) && b <= 0xFF ? [(byte)b] : null,
            "bytes16" => TryHex(value, out var v16) ? LittleEndian(v16, 2) : null,
            "bytes32" => TryHex(value, out var v32) ? LittleEndian(v32, 4) : null,
            "bytes64" => TryHex(value, out var v64) ? LittleEndian(v64, 8) : null,
            "float32" => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)
                ? BitConverter.GetBytes(f) : null,
            "float64" => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? BitConverter.GetBytes(d) : null,
            "utf8" => [.. Encoding.UTF8.GetBytes(value), 0],
            "utf16" => [.. Encoding.Unicode.GetBytes(value), 0, 0],
            _ => null, // mask / mask_jump32: pattern scans, not fixed writes
        };
        return bytes is { Length: > 0 } ? new PatchLine(rva, bytes) : null;
    }

    private static bool TryHex(string text, out long value)
    {
        var s = text.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return long.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private static byte[]? FromHexString(string text)
    {
        var s = text.Replace(" ", "", StringComparison.Ordinal);
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        if (s.Length == 0 || s.Length % 2 != 0) return null;
        try
        {
            return Convert.FromHexString(s);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static byte[] LittleEndian(long value, int size)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        return bytes[..size];
    }
}
