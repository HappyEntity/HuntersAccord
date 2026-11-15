using HuntersAccord.Core.Pkg;

namespace HuntersAccord.Core.Tests;

public class PfscImageTests
{
    [Theory]
    [InlineData(new byte[] { 0x78, 0x9C }, true)]  // default window
    [InlineData(new byte[] { 0x78, 0xDA }, true)]  // best compression
    [InlineData(new byte[] { 0x48, 0x89 }, true)]  // 4K window, as used by PS4 PFSC blocks
    [InlineData(new byte[] { 0x48, 0x8A }, false)] // bad checksum
    [InlineData(new byte[] { 0x0B, 0x2A }, false)] // not deflate
    public void Detects_zlib_headers(byte[] header, bool expected) =>
        Assert.Equal(expected, PfscImage.IsZlibHeader(header));
}
