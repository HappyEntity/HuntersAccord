using System.Buffers.Binary;
using System.Text;

namespace HuntersAccord.Core.Pkg;

/// <summary>Minimal reader for PS4 <c>param.sfo</c> (little-endian key/value table, magic <c>\0PSF</c>).</summary>
public sealed class ParamSfo
{
    private const uint Magic = 0x46535000; // "\0PSF" read as LE

    private readonly Dictionary<string, object> _values;

    private ParamSfo(Dictionary<string, object> values) => _values = values;

    public IReadOnlyDictionary<string, object> Values => _values;

    public string? GetString(string key) => _values.GetValueOrDefault(key) as string;

    public uint? GetUInt(string key) => _values.GetValueOrDefault(key) is uint v ? v : null;

    public string? TitleId => GetString("TITLE_ID");
    public string? Title => GetString("TITLE");
    public string? AppVersion => GetString("APP_VER");
    public string? Version => GetString("VERSION");

    /// <summary><c>gd</c> = full game, <c>gp</c> = patch, <c>ac</c> = additional content.</summary>
    public string? Category => GetString("CATEGORY");

    public bool IsPatch => Category is { } c && c.StartsWith("gp", StringComparison.Ordinal);

    public static ParamSfo Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 0x14 || BinaryPrimitives.ReadUInt32LittleEndian(data) != Magic)
            throw new InvalidDataException("Invalid param.sfo.");

        var keyTable = (int)BinaryPrimitives.ReadUInt32LittleEndian(data[0x08..]);
        var dataTable = (int)BinaryPrimitives.ReadUInt32LittleEndian(data[0x0C..]);
        var count = (int)BinaryPrimitives.ReadUInt32LittleEndian(data[0x10..]);

        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var row = data[(0x14 + i * 16)..];
            var keyOffset = BinaryPrimitives.ReadUInt16LittleEndian(row);
            var format = BinaryPrimitives.ReadUInt16LittleEndian(row[2..]);
            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(row[4..]);
            var dataOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(row[12..]);

            var keySpan = data[(keyTable + keyOffset)..];
            var key = Encoding.UTF8.GetString(keySpan[..keySpan.IndexOf((byte)0)]);
            var value = data.Slice(dataTable + dataOffset, length);

            values[key] = format switch
            {
                0x0404 => BinaryPrimitives.ReadUInt32LittleEndian(value),
                0x0204 => Encoding.UTF8.GetString(value).TrimEnd('\0'),
                _ => value.ToArray(),
            };
        }
        return new ParamSfo(values);
    }
}
