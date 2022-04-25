using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git
{
    public class GitCacheOptions
    {
        public GitIdType IdType { get; set; }

        public bool LookForEndOfIndex { get; set; }
    }

    public record GitCacheEntry
    {
        public string Name { get; init; } = default!;
        public GitTreeElementType Type { get; init; }
        public GitId Id { get; init; } = default!;

        public int CreationTimeSeconds { get; init; }
        public int CreationTimeNano { get; init; }
        public int ModificationTimeSeconds { get; init; }
        public int ModificationTimeNano { get; init; }
        public int DeviceId { get; init; }
        public int INodeId { get; init; }

        public int UserId { get; init; }
        public int GroupId { get; init; }
        public int TruncatedSizeOnDisk { get; init; }
        public int Flags { get; init; }        
    }

    /// <summary>
    /// Git Directory Cache (aka "INDEX") bucket
    /// </summary>
    public class GitCacheBucket : GitBucket
    {
        readonly GitIdType _idType;
        readonly bool _lookForEndOfIndex;
        int _version;
        int _indexCount;
        long _indexStart;
        long? _endOfIndex;
        int _skip;
        int _nRead;
        string? _lastName;

        public static readonly int LowestSupportedFormat = 2;
        public static readonly int HighestSupportedFormat = 4;

        public int? IndexVersion => _version > 0 ? _version : null;

        public GitCacheBucket(Bucket inner, GitCacheOptions? options = null) : base(inner)
        {
            _idType = options?.IdType ?? GitIdType.Sha1;
            _lookForEndOfIndex = options?.LookForEndOfIndex ?? false;
        }

        public async ValueTask ReadHeader()
        {
            if (_version > 0)
                return;
            var bb = await Inner.ReadFullAsync(12).ConfigureAwait(false);

            if (!bb.StartsWithASCII("DIRC"))
                throw new GitBucketException($"No Directory cache in {Name} bucket");

            _version = NetBitConverter.ToInt32(bb, 4);
            if (_version < LowestSupportedFormat || _version > HighestSupportedFormat)
                throw new GitBucketException($"Unsupported Directory Cache (aka INDEX) version {_version} in {Name} bucket");

            _indexCount = NetBitConverter.ToInt32(bb, 8);
            _indexStart = 12;

            if (_lookForEndOfIndex && Inner is FileBucket fb)
            {
                var endOfIndex = new byte[4 /* HDR */ + sizeof(uint) /* Size */ + sizeof(uint) /* Offset */ + 20 /* SHA-1 hash */];
                long pos = fb.Length - _idType.HashLength() - endOfIndex.Length;
                
                if (pos > 0 && endOfIndex.Length == await fb.ReadAtAsync(pos, endOfIndex).ConfigureAwait(false)
                    && (bb = endOfIndex).StartsWithASCII("EOIE")
                    && NetBitConverter.ToInt32(bb, 4) == endOfIndex.Length-8)
                {
                    _endOfIndex = NetBitConverter.ToInt32(bb, 8);
                }
            }
        }

        public async ValueTask<GitCacheEntry?> ReadEntryAsync()
        {
            if (_indexStart == 0)
                await ReadHeader().ConfigureAwait(false);

            while (_skip > 0)
            {
                int n = await Inner.ReadSkipAsync(_skip).ConfigureAwait(false);

                if (n == 0)
                    throw new GitBucketException($"Unexpected EOF in {Name}");
                _skip -= n;
            }

            if (_nRead >= _indexCount)
                return null;
            _nRead++;

            var bb = await Inner.ReadFullAsync(42 + _idType.HashLength()).ConfigureAwait(false);

            if (bb.IsEof)
                return null;

            GitCacheEntry src = new()
            {
                CreationTimeSeconds = NetBitConverter.ToInt32(bb, 0),
                CreationTimeNano = NetBitConverter.ToInt32(bb, 4),
                ModificationTimeSeconds = NetBitConverter.ToInt32(bb, 8),
                ModificationTimeNano = NetBitConverter.ToInt32(bb, 12),
                DeviceId = NetBitConverter.ToInt32(bb, 16),
                INodeId = NetBitConverter.ToInt32(bb, 20),
                Type = (GitTreeElementType)NetBitConverter.ToInt32(bb, 24),
                UserId = NetBitConverter.ToInt32(bb, 28),
                GroupId = NetBitConverter.ToInt32(bb, 32),
                TruncatedSizeOnDisk = NetBitConverter.ToInt32(bb, 36),
                Flags = NetBitConverter.ToInt16(bb, 40) << 16 | _version,
                Id = new GitId(_idType, bb.Slice(42).ToArray())
            };

            if (_version < 4)
            {
                var (name, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.Zero).ConfigureAwait(false);

                _skip = 8 - (int)((Inner.Position!.Value - _indexStart) & 0x7);

                if (_skip == 8)
                    _skip = 0;

                return src with { Name = name.ToUTF8String(eol) };
            }
            else
            {
                int drop = (int)await Inner.ReadGitOffsetAsync().ConfigureAwait(false);
                var (name, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.Zero).ConfigureAwait(false);

                string sName;

                int len = (_lastName?.Length - drop) ?? 0;

                if (len > 0)
                {
                    if (!_lastName.AsSpan(0, len).Any(x => x >= 0x80))
                    {
#if !NETFRAMEWORK
                        _lastName = sName = string.Concat(_lastName.AsSpan(0,  len), name.ToUTF8String(eol));
#else
                        _lastName = sName = string.Concat(_lastName.Substring(0, len), name.ToUTF8String(eol));
#endif
                    }
                    else
                    {                        
                        name = Encoding.UTF8.GetBytes(_lastName!).Take(len).Concat(name.ToArray()).ToArray();
                        _lastName = sName = name.ToUTF8String(eol);
                    }
                }
                else
                     _lastName = sName = name.ToUTF8String(eol);

                return src with { Name = sName };
            }
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            await ReadHeader().ConfigureAwait(false);

            while(0 != await Inner.ReadSkipAsync(int.MaxValue).ConfigureAwait(false))
            {

            }

            return BucketBytes.Eof;
        }
    }
}
