using System.Buffers.Binary;
using System.Text;

namespace HuntersAccord.Core.Pkg;

public sealed record PfsFile(string RelativePath, long Offset, long Size);

/// <summary>
/// Parses the inner (decompressed) PFS: superblock, inode table and directory tree under <c>uroot</c>.
/// Files are stored contiguously starting at their first data block.
/// </summary>
public sealed class PfsFileSystem
{
    private const int InodeSize = 0xA8;
    private const int InodeFirstBlockOffset = 0x64;

    private const uint DirentFile = 2;
    private const uint DirentDirectory = 3;

    private readonly PfscReader _reader;
    private readonly List<(ushort Mode, long Size, uint FirstBlock)> _inodes = [];

    private PfsFileSystem(PfscReader reader) => _reader = reader;

    public int BlockSize { get; private set; }

    public IReadOnlyList<string> Directories { get; private set; } = [];

    public IReadOnlyList<PfsFile> Files { get; private set; } = [];

    public static PfsFileSystem Load(PfscReader reader)
    {
        var fs = new PfsFileSystem(reader);
        fs.Parse();
        return fs;
    }

    private void Parse()
    {
        var super = new byte[0x60];
        _reader.Read(0, super);
        if (BinaryPrimitives.ReadUInt64LittleEndian(super.AsSpan(8)) != PfsImage.SuperblockMagic)
            throw new InvalidDataException("Inner PFS superblock not found — decryption failed.");

        var mode = BinaryPrimitives.ReadUInt16LittleEndian(super.AsSpan(0x1C));
        if ((mode & 1) != 0) throw new NotSupportedException("Signed inner PFS images are not supported.");

        BlockSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(super.AsSpan(0x20));
        var inodeCount = (long)BinaryPrimitives.ReadUInt64LittleEndian(super.AsSpan(0x30));
        var superRoot = (int)BinaryPrimitives.ReadUInt64LittleEndian(super.AsSpan(0x48));

        // Inodes start at block 1 and never straddle a block boundary.
        var perBlock = BlockSize / InodeSize;
        var block = new byte[BlockSize];
        for (long i = 0; i < inodeCount; i++)
        {
            var slot = (int)(i % perBlock);
            if (slot == 0) _reader.Read((1 + i / perBlock) * BlockSize, block);
            var inode = block.AsSpan(slot * InodeSize, InodeSize);
            _inodes.Add((
                BinaryPrimitives.ReadUInt16LittleEndian(inode),
                BinaryPrimitives.ReadInt64LittleEndian(inode[0x08..]),
                BinaryPrimitives.ReadUInt32LittleEndian(inode[InodeFirstBlockOffset..])));
        }

        var uroot = ReadDirectory(superRoot).FirstOrDefault(d => d.Name == "uroot");
        if (uroot.Name is null) throw new InvalidDataException("PFS has no 'uroot' directory.");

        var dirs = new List<string>();
        var files = new List<PfsFile>();
        Walk(uroot.Inode, "", dirs, files);
        Directories = dirs;
        Files = files;
    }

    private void Walk(int dirInode, string prefix, List<string> dirs, List<PfsFile> files)
    {
        foreach (var (name, inode, type) in ReadDirectory(dirInode))
        {
            var path = prefix.Length == 0 ? name : prefix + "/" + name;
            if (type == DirentDirectory)
            {
                dirs.Add(path);
                Walk(inode, path, dirs, files);
            }
            else if (type == DirentFile)
            {
                var node = _inodes[inode];
                files.Add(new PfsFile(path, (long)node.FirstBlock * BlockSize, node.Size));
            }
        }
    }

    private List<(string Name, int Inode, uint Type)> ReadDirectory(int inode)
    {
        var node = _inodes[inode];
        var size = (int)((node.Size + BlockSize - 1) / BlockSize * BlockSize);
        var data = new byte[size];
        _reader.Read((long)node.FirstBlock * BlockSize, data);

        var entries = new List<(string, int, uint)>();
        for (var blockStart = 0; blockStart < size; blockStart += BlockSize)
        {
            var pos = blockStart;
            while (pos + 16 <= blockStart + BlockSize)
            {
                var d = data.AsSpan(pos);
                var ino = (int)BinaryPrimitives.ReadUInt32LittleEndian(d);
                var type = BinaryPrimitives.ReadUInt32LittleEndian(d[4..]);
                var nameLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(d[8..]);
                var entSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(d[12..]);
                if (entSize == 0) break; // rest of this block is unused

                if (type is DirentFile or DirentDirectory)
                    entries.Add((Encoding.UTF8.GetString(d.Slice(16, nameLen)), ino, type));
                pos += entSize;
            }
        }
        return entries;
    }
}
