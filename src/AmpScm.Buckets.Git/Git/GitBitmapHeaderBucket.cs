using System.Diagnostics;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git;

[DebuggerDisplay("{BitmapType}, Version={Version}, Flags={Flags}, ObjectCount={ObjectCount}")]
public class GitBitmapHeaderBucket : GitBucket
{
    private GitIdType _idType;
    private bool _readHeader;
    private string? _type;
    private short _version;
    private short _flags;
    private int _objCount;
    private GitId? _checksum;

    public GitBitmapHeaderBucket(Bucket source, GitIdType idType) : base(source)
    {
        _idType = idType;
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        if (!_readHeader)
        {
            var bb = await Source.ReadExactlyAsync(12).ConfigureAwait(false);

            if (bb.Length != 12)
                throw new BucketEofException(Source);

            _type = bb.ToASCIIString(0, 4);
            _version = NetBitConverter.ToInt16(bb, 4);
            _flags = NetBitConverter.ToInt16(bb, 6);
            _objCount = NetBitConverter.ToInt32(bb, 8);

            _checksum = await Source.ReadGitIdAsync(_idType).ConfigureAwait(false);
            _readHeader = true;
        }

        return BucketBytes.Eof;
    }

    public string? BitmapType => _type;
    public int? Version => _readHeader ? _version : null;
    public int? Flags => _readHeader ? _flags : null;
    public int? ObjectCount => _readHeader ? _objCount : null;

    protected GitId? Checksum => _checksum;
}
