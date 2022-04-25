using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        long _length;
        bool _processedExtensions;

        public static readonly int LowestSupportedFormat = 2;
        public static readonly int HighestSupportedFormat = 4;

        public int? IndexVersion => _version > 0 ? _version : null;

        public GitCacheBucket(Bucket inner, GitCacheOptions? options = null) : base(inner)
        {
            _idType = options?.IdType ?? GitIdType.Sha1;
            _lookForEndOfIndex = options?.LookForEndOfIndex ?? false;
        }

        public async ValueTask ReadHeaderAsync()
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

            if (Inner is FileBucket fb)
            {
                _length = fb.Length;
                if (_lookForEndOfIndex)
                {
                    var endOfIndex = new byte[4 /* HDR */ + sizeof(uint) /* Size */ + sizeof(uint) /* Offset */ + 20 /* SHA-1 hash */];

                    long pos = _length - _idType.HashLength() - endOfIndex.Length;

                    if (pos > 0 && endOfIndex.Length == await fb.ReadAtAsync(pos, endOfIndex).ConfigureAwait(false)
                        && (bb = endOfIndex).StartsWithASCII("EOIE")
                        && NetBitConverter.ToInt32(bb, 4) == endOfIndex.Length - 8)
                    {
                        _endOfIndex = NetBitConverter.ToInt32(bb, 8);
                    }
                }
            }
            else
            {
                _length = await Inner.ReadRemainingBytesAsync().ConfigureAwait(false) + Inner.Position ?? -1;
            }
        }

        public async ValueTask<GitCacheEntry?> ReadEntryAsync()
        {
            if (_indexStart == 0)
                await ReadHeaderAsync().ConfigureAwait(false);

            while (_skip > 0)
            {
                int n = await Inner.ReadSkipAsync(_skip).ConfigureAwait(false);

                if (n == 0)
                    throw new GitBucketException($"Unexpected EOF in {Name}");
                _skip -= n;
            }

            if (_nRead >= _indexCount)
            {
                if (!_endOfIndex.HasValue)
                    _endOfIndex = Inner.Position;

                return null;
            }
            _nRead++;

            int readLen = 42 + _idType.HashLength();
            var bb = await Inner.ReadFullAsync(readLen).ConfigureAwait(false);

            if (bb.IsEof)
                return null;
            else if (bb.Length != readLen)
                throw new GitBucketException($"Unexpected EOF in {Name} Bucket");

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

                if (eol != BucketEol.Zero)
                    throw new GitBucketException($"Unexpected EOF in {Name} Bucket");

                _skip = 8 - (int)((Inner.Position!.Value - _indexStart) & 0x7);

                if (_skip == 8)
                    _skip = 0;

                return src with { Name = name.ToUTF8String(eol) };
            }
            else
            {
                int drop = (int)await Inner.ReadGitOffsetAsync().ConfigureAwait(false);
                var (name, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.Zero).ConfigureAwait(false);

                if (eol != BucketEol.Zero)
                    throw new GitBucketException($"Unexpected EOF in {Name} Bucket");

                string sName;

                int len = (_lastName?.Length - drop) ?? 0;

                if (len > 0)
                {
                    if (!_lastName.AsSpan(0, len).Any(x => x >= 0x80))
                    {
#if !NETFRAMEWORK
                        _lastName = sName = string.Concat(_lastName.AsSpan(0, len), name.ToUTF8String(eol));
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

        public async ValueTask ProcessExtensionsAsync()
        {
            if (_processedExtensions)
                return;

            if (_nRead < _indexCount && !_endOfIndex.HasValue)
            {
                while (null != await ReadEntryAsync().ConfigureAwait(false))
                {
                }
            }

            if (!_endOfIndex.HasValue)
                throw new InvalidOperationException();

            Bucket reader;
            if (_nRead == _indexCount)
                reader = Inner.NoClose(true);
            else
            {
                reader = await Inner.DuplicateAsync(true).ConfigureAwait(false);

                await reader.SeekAsync(_endOfIndex.Value).ConfigureAwait(false);
            }

            using (reader)
            {
                long bucketEnd = _length - _idType.HashLength();

                while (reader.Position < bucketEnd)
                {
                    var bb = await reader.ReadFullAsync(4 + 4).ConfigureAwait(false);

                    if (bb.Length != 4 + 4)
                        throw new GitBucketException($"Unexpected EOF in {Name} Bucket");

                    string extensionName = bb.ToUTF8String(0, 4);

                    switch (extensionName)
                    {
                        case "EOIE": // End of Index Entry.
                            break;

                        case "TREE": // Cache Tree
                            break;

                        case "REUC": // Resolve undo
                            break;

                        case "UNTR": // Untracked cache
                            break;

                        case "FSMN": // File System Monitor cache
                            break;

                        case "sdir": // Sparse Directory Entries
                            break; // Sparse directory entries may exist. Fine!

                        default:
                            if (extensionName[0] < 'A' || extensionName[0] > 'Z')
                                throw new GitBucketException($"Unknown non optional extension '{extensionName}' in {Name} bucket");

                            Debug.WriteLine($"Ignoring unknown extension '{extensionName}' in {Name} bucket");
                            break;
                    }

                    int extensionlen = NetBitConverter.ToInt32(bb, 4);

                    if (extensionlen != await reader.ReadSkipAsync(extensionlen).ConfigureAwait(false))
                        throw new GitBucketException($"Unexpected EOF in '{extensionName}' extension of {Name} bucket");
                }
                _processedExtensions = true;
            }
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            await ReadHeaderAsync().ConfigureAwait(false);

            while (null != await ReadEntryAsync().ConfigureAwait(false))
            {

            }

            await ProcessExtensionsAsync().ConfigureAwait(false);

            while (0 != await Inner.ReadSkipAsync(int.MaxValue).ConfigureAwait(false))
            {

            }

            return BucketBytes.Eof;
        }
    }
}
