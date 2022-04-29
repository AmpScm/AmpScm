using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git
{
    public sealed class GitDirectoryOptions
    {
        public GitIdType IdType { get; set; } = GitIdType.Sha1;

        public bool LookForEndOfIndex { get; set; }
        public bool PreLoadExtensions { get; set; }
    }

    public sealed record GitDirectoryEntry : IComparable<GitDirectoryEntry>
    {
        public string Name { get; init; } = default!;
        public GitTreeElementType Type { get; init; }
        public GitId Id { get; init; } = default!;
        public int Flags { get; init; }

        public DateTime CreationTime => (DateTimeOffset.FromUnixTimeSeconds(UnixCreationTime) + new TimeSpan(UnixCreationTimeNano / 100)).DateTime;
        public DateTime ModificationTime => (DateTimeOffset.FromUnixTimeSeconds(UnixModificationTime) + new TimeSpan(UnixModificationTimeNano / 100)).DateTime;

        public int Stage => (Flags & 0x3000) >> 12;

        public int DeviceId { get; init; }
        public int INodeId { get; init; }

        public int UserId { get; init; }
        public int GroupId { get; init; }
        [CLSCompliant(false)]
        public uint TruncatedFileSize { get; init; }

        public int UnixCreationTime { get; init; }
        public int UnixCreationTimeNano { get; init; }
        public int UnixModificationTime { get; init; }
        public int UnixModificationTimeNano { get; init; }

        public int CompareTo(GitDirectoryEntry? other)
        {
            int n = string.CompareOrdinal(Name, other?.Name);

            if (n == 0)
                return Stage - (other?.Stage ?? 0);
            else
                return n;
        }

        public static bool operator <(GitDirectoryEntry left, GitDirectoryEntry right)
        {
            return ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;
        }

        public static bool operator <=(GitDirectoryEntry left, GitDirectoryEntry right)
        {
            return ReferenceEquals(left, null) || left.CompareTo(right) <= 0;
        }

        public static bool operator >(GitDirectoryEntry left, GitDirectoryEntry right)
        {
            return !ReferenceEquals(left, null) && left.CompareTo(right) > 0;
        }

        public static bool operator >=(GitDirectoryEntry left, GitDirectoryEntry right)
        {
            return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;
        }
    }

    /// <summary>
    /// Git Directory Cache (aka "INDEX") bucket
    /// </summary>
    public sealed class GitDirectoryBucket : GitBucket
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
        bool _preLoadExtensions;
        public static readonly int LowestSupportedFormat = 2;
        public static readonly int HighestSupportedFormat = 4;
        string? _directory;
        long? _firstReal;
        int _firstRealIdx;
        GitEwahBitmapBucket? _deleted;
        GitEwahBitmapBucket? _replaced;
        GitDirectoryBucket? _shared;

        public int? IndexVersion => _version > 0 ? _version : null;

        public GitDirectoryBucket(Bucket inner, GitDirectoryOptions? options) : base(inner)
        {
            _idType = options?.IdType ?? GitIdType.Sha1;
            _lookForEndOfIndex = options?.LookForEndOfIndex ?? false;
            _preLoadExtensions = options?.PreLoadExtensions ?? false;
        }

        public GitDirectoryBucket(Bucket inner)
            : this(inner, options: null)
        {
        }

        public GitDirectoryBucket(string gitDirectory, GitDirectoryOptions? options)
            : this(FileBucket.OpenRead(Path.Combine(
                string.IsNullOrEmpty(gitDirectory) ? throw new ArgumentNullException(nameof(gitDirectory)) : gitDirectory, "index")), options)
        {
            _directory = gitDirectory;

            if (Directory.EnumerateFiles(gitDirectory, "sharedindex.*").Any())
            {
                _lookForEndOfIndex = true;
                _preLoadExtensions = true;
            }
        }

        public GitDirectoryBucket(string gitDirectory)
            : this(gitDirectory, options: null)
        {

        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    _deleted?.Dispose();
                    _replaced?.Dispose();
                    _shared?.Dispose();
                    _remaining?.Dispose();
                }
            }
            finally
            {
                _deleted = null;
                _replaced = null;
                _shared = null;
                _remaining = null;

                base.Dispose(disposing);
            }
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

            if (_preLoadExtensions)
            {
                await ProcessExtensionsAsync().ConfigureAwait(false);
                await Inner.SeekAsync(_indexStart).ConfigureAwait(false);
                _nRead = 0;
                _lastName = null;
            }
        }

        public ValueTask<GitDirectoryEntry?> ReadEntryAsync()
        {
            if (_shared is null)
                return ReadEntryDirectAsync();
            else if (_firstRealIdx < _indexCount)
                return ReadMixedAsync();
            else
                return ReadEntryLinkAsync();
        }

        async ValueTask<GitDirectoryEntry?> ReadEntryDirectAsync(bool allowSplit = false)
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

                if (!_firstReal.HasValue)
                {
                    _firstReal = Inner.Position;
                    _firstRealIdx = _indexCount;
                }

                await ProcessExtensionsAsync().ConfigureAwait(false);

                return null;
            }
            _nRead++;

            int hashLen = _idType.HashLength();
            int readLen = 40 + 2 + hashLen;
            var bb = await Inner.ReadFullAsync(readLen).ConfigureAwait(false);

            if (bb.Length != readLen)
                throw new GitBucketException($"Unexpected EOF in {Name} Bucket");

            GitDirectoryEntry src = new()
            {
                UnixCreationTime = NetBitConverter.ToInt32(bb, 0),
                UnixCreationTimeNano = NetBitConverter.ToInt32(bb, 4),
                UnixModificationTime = NetBitConverter.ToInt32(bb, 8),
                UnixModificationTimeNano = NetBitConverter.ToInt32(bb, 12),
                DeviceId = NetBitConverter.ToInt32(bb, 16),
                INodeId = NetBitConverter.ToInt32(bb, 20),
                Type = (GitTreeElementType)NetBitConverter.ToInt32(bb, 24),
                UserId = NetBitConverter.ToInt32(bb, 28),
                GroupId = NetBitConverter.ToInt32(bb, 32),
                TruncatedFileSize = NetBitConverter.ToUInt32(bb, 36),
                Id = new GitId(_idType, bb.Slice(40, hashLen).ToArray()),
                Flags = NetBitConverter.ToInt16(bb, 40 + hashLen),
            };

            int nameLen = (src.Flags & 0xFFF);

            if (nameLen != 0 && !_firstReal.HasValue)
            {
                _firstReal = Inner.Position - bb.Length;
                _firstRealIdx = _nRead - 1;
            }

            int FullFlags = src.Flags;
            if ((src.Flags & 0x4000) != 0 && _version >= 3) // Must be 0 in version 2
            {
                bb = await Inner.ReadFullAsync(2).ConfigureAwait(false);

                if (bb.Length != 2)
                    throw new GitBucketException($"Unexpected EOF in {Name} Bucket");

                FullFlags |= NetBitConverter.ToInt16(bb, 0) << 16;
            }

            if (_version < 4)
            {
                var (name, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.Zero).ConfigureAwait(false);

                if (eol != BucketEol.Zero)
                    throw new GitBucketException($"Unexpected EOF in {Name} Bucket");

                _skip = 8 - (int)((Inner.Position!.Value - _indexStart) & 0x7);

                if (_skip == 8)
                    _skip = 0;

                if (nameLen == 0 && !_firstReal.HasValue)
                {
                    if (_directory is null)
                        throw new GitBucketException("Can't load split index when path is not passed");
                    else if (_firstReal.HasValue)
                        throw new GitBucketException("Can't read split entry after normal entries");

                    return await ReadAsSplitAsync().ConfigureAwait(false);
                }

                return src with { Name = name.ToUTF8String(eol), Flags = FullFlags };
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
                        _lastName = sName = string.Concat(_lastName!.Substring(0, len), name.ToUTF8String(eol));
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

                if (nameLen == 0 && !_firstReal.HasValue && !allowSplit)
                {
                    if (_directory is null)
                        throw new GitBucketException("Can't load split index when path is not passed");

                    return await ReadAsSplitAsync().ConfigureAwait(false);
                }

                return src with { Name = sName, Flags = FullFlags };
            }
        }

        private async ValueTask<GitDirectoryEntry?> ReadAsSplitAsync()
        {
            if (!_processedExtensions || !_firstReal.HasValue)
            {
                // Extensions were not pre-processed, so we need to process them now
                await ProcessExtensionsAsync().ConfigureAwait(false);
                //while (null != await ReadEntryDirectAsync(true).ConfigureAwait(false))
                //{
                //
                //}

                if (!_firstReal.HasValue)
                {
                    _firstReal = _endOfIndex;
                    _firstRealIdx = _indexCount;
                }

                if (_deleted is null)
                    throw new GitBucketException("Couldn't load shared index needed for loading split index");
            }

            _nRead = 0;
            _lastName = null;
            await Inner.SeekAsync(_indexStart).ConfigureAwait(false);

            return await ReadEntryAsync().ConfigureAwait(false);
        }

#pragma warning disable CA2213 // Disposable fields should be disposed
        IAsyncEnumerator<bool>? _deletedWalk;
        IAsyncEnumerator<bool>? _replacedWalk;
#pragma warning restore CA2213 // Disposable fields should be disposed
        async ValueTask<GitDirectoryEntry?> ReadEntryLinkAsync()
        {
            while (true)
            {
                var shared = await _shared!.ReadEntryAsync().ConfigureAwait(false);
                if (shared is null)
                    break;

                if (_deletedWalk is null || _replacedWalk is null)
                {
                    if (_firstRealIdx == 0)
                        _firstRealIdx = await _replaced!.ReadBitLengthAsync().ConfigureAwait(false);

                    _deletedWalk = _deleted!.AllBits.GetAsyncEnumerator();
                    _replacedWalk = _replaced!.AllBits.GetAsyncEnumerator();
                }

                bool deleted = (await _deletedWalk.MoveNextAsync().ConfigureAwait(false) && _deletedWalk.Current);
                bool replaced = (await _replacedWalk!.MoveNextAsync().ConfigureAwait(false) && _replacedWalk.Current);

                if (deleted)
                    continue;
                else if (replaced)
                {
                    var me = await ReadEntryDirectAsync(true).ConfigureAwait(false);

                    return me! with { Name = shared.Name };
                }
                else
                    return shared;
            }

            if (_deletedWalk is not null)
            {
                await _deletedWalk.DisposeAsync().ConfigureAwait(false);
                _deletedWalk = null;
            }
            if (_replacedWalk is not null)
            {
                await _replacedWalk.DisposeAsync().ConfigureAwait(false);
                _deletedWalk = null;
            }

            return null;
        }

        GitDirectoryBucket? _remaining;
        GitDirectoryEntry? _linkEntry;
        GitDirectoryEntry? _remainingEntry;
        async ValueTask<GitDirectoryEntry?> ReadMixedAsync()
        {
            if (_remaining is null && _firstReal.HasValue)
            {
                Bucket b = await Inner.DuplicateAsync().ConfigureAwait(false)!;
                await b.SeekAsync(_firstReal.Value).ConfigureAwait(false);

                _remaining = new GitDirectoryBucket(b);
                _remaining._nRead = _firstRealIdx;
                _remaining._lastName = null;
                _remaining._processedExtensions = true;
                _remaining._indexCount = _indexCount;
                _remaining._version = _version;
            }

            _linkEntry ??= await ReadEntryLinkAsync().ConfigureAwait(false);
            if (_remaining is not null)
                _remainingEntry ??= await _remaining.ReadEntryAsync().ConfigureAwait(false);

            if (_linkEntry is not null && _remainingEntry is not null)
            {
                if (0 >= _linkEntry.CompareTo(_remainingEntry))
                {
                    var r = _linkEntry;
                    _linkEntry = null;
                    return r;
                }
                else
                {
                    var r = _remainingEntry;
                    _remainingEntry = null;
                    return r;
                }
            }
            else if (_linkEntry is not null)
            {
                var r = _linkEntry;
                _linkEntry = null;
                return r;
            }
            else
            {
                var r = _remainingEntry;
                _remainingEntry = null;
                return r;
            }
        }

        public async ValueTask ProcessExtensionsAsync()
        {
            if (_processedExtensions)
                return;

            if (_nRead < _indexCount && !_endOfIndex.HasValue)
            {
                while (null != await ReadEntryDirectAsync(true).ConfigureAwait(false))
                {
                }
            }

            if (!_endOfIndex.HasValue)
                throw new InvalidOperationException();

            _processedExtensions = true;

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

                    int extensionlen = NetBitConverter.ToInt32(bb, 4);


                    switch (extensionName)
                    {
                        case "EOIE": // End of Index Entry.
                            break;

                        case "link":
                            if (!_preLoadExtensions)
                                throw new GitBucketException("Can't load split index when PreLoadExtensions is not set");

                            await LoadLinkAsync(reader, extensionlen).ConfigureAwait(false);

                            continue;

                        case "TREE": // Cache Tree
                            break;

                        case "REUC": // Resolve undo
                            break;

                        case "UNTR": // Untracked cache
                            break;

                        case "FSMN": // File System Monitor cache
                            break;

                        case "sdir": // Sparse Directory Entries
                            break; // Sparse directory entries may exist. Fine. We already assumed that!

                        default:
                            if (extensionName[0] < 'A' || extensionName[0] > 'Z')
                                throw new GitBucketException($"Unknown non optional extension '{extensionName}' in {Name} bucket");

                            Debug.WriteLine($"Ignoring unknown extension '{extensionName}' in {Name} bucket");
                            break;
                    }

                    if (extensionlen > 0)
                    {
                        if (extensionlen != await reader.ReadSkipAsync(extensionlen).ConfigureAwait(false))
                            throw new GitBucketException($"Unexpected EOF in '{extensionName}' extension of {Name} bucket");
                    }
                }
            }
        }

        private async ValueTask LoadLinkAsync(Bucket reader, int extensionLength)
        {
            long endPos = reader.Position!.Value + extensionLength;

            GitId shared = await reader.ReadGitIdAsync(_idType).ConfigureAwait(false);

            _deleted = new GitEwahBitmapBucket(await reader.DuplicateAsync().ConfigureAwait(false));
            int delLength = await _deleted.ReadLengthAsync().ConfigureAwait(false);

            if (await reader.ReadSkipAsync(delLength).ConfigureAwait(false) != delLength)
                throw new GitBucketException($"Unexpected EOF on {Name} Bucket");

            _replaced = new GitEwahBitmapBucket(await reader.DuplicateAsync().ConfigureAwait(false));
#if DEBUG
            int replaceLength = await _replaced.ReadLengthAsync().ConfigureAwait(false);

            Debug.Assert(delLength + replaceLength + _idType.HashLength() == extensionLength);
#endif
            await reader.SeekAsync(endPos).ConfigureAwait(false);

            if (!shared.IsZero)
                _shared = new GitDirectoryBucket(FileBucket.OpenRead(Path.Combine(_directory!, $"sharedindex.{shared}")), new GitDirectoryOptions() { IdType = _idType, LookForEndOfIndex = _lookForEndOfIndex });


            if (!_firstReal.HasValue)
            {
                while (await ReadEntryDirectAsync(true).ConfigureAwait(false) is GitDirectoryEntry entry)
                {
                    if (!string.IsNullOrEmpty(entry.Name))
                        break;
                }

                Debug.Assert(_firstReal.HasValue);
            }
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            await ProcessExtensionsAsync().ConfigureAwait(false);

            while (0 != await Inner.ReadSkipAsync(int.MaxValue).ConfigureAwait(false))
            {
            }

            return BucketBytes.Eof;
        }
    }
}
