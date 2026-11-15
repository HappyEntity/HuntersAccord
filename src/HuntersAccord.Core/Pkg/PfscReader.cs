namespace HuntersAccord.Core.Pkg;

/// <summary>
/// Random-access reader over the decompressed inner image, caching the last block.
/// One instance per thread (owns an XTS decryptor and buffers).
/// </summary>
public sealed class PfscReader : IDisposable
{
    private readonly PfscImage _image;
    private readonly XtsAes128 _xts;
    private readonly byte[] _block;
    private readonly byte[] _scratch;
    private int _cachedBlock = -1;

    public PfscReader(PfscImage image)
    {
        _image = image;
        _xts = image.Outer.CreateDecryptor();
        _block = new byte[image.BlockSize];
        _scratch = new byte[image.BlockSize * 2];
    }

    public PfscImage Image => _image;

    public void Read(long offset, Span<byte> destination)
    {
        while (!destination.IsEmpty)
        {
            var index = (int)(offset / _image.BlockSize);
            var within = (int)(offset % _image.BlockSize);
            if (index >= _image.BlockCount) throw new EndOfStreamException("Read past the end of the PFS image.");

            if (within == 0 && destination.Length >= _image.BlockSize)
            {
                // Whole block straight into the caller's buffer, no copy.
                _image.ReadBlock(index, destination[.._image.BlockSize], _xts, _scratch);
                destination = destination[_image.BlockSize..];
                offset += _image.BlockSize;
                continue;
            }

            if (_cachedBlock != index)
            {
                _image.ReadBlock(index, _block, _xts, _scratch);
                _cachedBlock = index;
            }
            var n = Math.Min(destination.Length, _image.BlockSize - within);
            _block.AsSpan(within, n).CopyTo(destination);
            destination = destination[n..];
            offset += n;
        }
    }

    public void Dispose() => _xts.Dispose();
}
