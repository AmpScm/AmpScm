using System.Diagnostics;
using System.Text;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git;

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
    private readonly GitIdType _idType;
    private readonly bool _lookForEndOfIndex;
    private int _version;
    private int _indexCount;
    private long _indexStart;
    private long? _endOfIndex;
    private int _skip;
    private int _nRead;
    private string? _lastName;
    private long _length;
    private bool _processedExtensions;
    private bool _preLoadExtensions;
    public static readonly int LowestSupportedFormat = 2;
    public static readonly int HighestSupportedFormat = 4;
    private string? _directory;
    private long? _firstReal;
    private int _firstRealIdx;
    private GitEwahBitmapBucket? _deleted;
    private GitEwahBitmapBucket? _replaced;
    private GitDirectoryBucket? _shared;

    public int? IndexVersion => _version > 0 ? _version : null;

    public GitDirectoryBucket(Bucket source, GitDirectoryOptions? options) : base(source)
    {
        _idType = options?.IdType ?? GitIdType.Sha1;
        _lookForEndOfIndex = options?.LookForEndOfIndex ?? false;
        _preLoadExtensions = options?.PreLoadExtensions ?? false;
    }

    public GitDirectoryBucket(Bucket source)
        : this(source, options: null)
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

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        try
        {
            if (disposing)
            {
                if (_deleted is { })
                    await _deleted.DisposeAsync().ConfigureAwait(false);
                if (_replaced is { })
                    await _replaced.DisposeAsync().ConfigureAwait(false);
                if (_shared is { })
                    await _shared.DisposeAsync().ConfigureAwait(false);
                if (_remaining is { })
                    await _remaining.DisposeAsync();
            }
        }
        finally
        {
            _deleted = null;
            _replaced = null;
            _shared = null;
            _remaining = null;

            await base.DisposeAsync(disposing).ConfigureAwait(false);
        }
    }

    public async ValueTask ReadHeaderAsync()
    {
        if (_version > 0)
            return;
        var bb = await Source.ReadAtLeastAsync(12).ConfigureAwait(false);

        if (!bb.StartsWithASCII("DIRC"))
            throw new GitBucketException($"No Directory cache in {Name} bucket");

        _version = NetBitConverter.ToInt32(bb, 4);
        if (_version < LowestSupportedFormat || _version > HighestSupportedFormat)
            throw new GitBucketException($"Unsupported Directory Cache (aka INDEX) version {_version} in {Name} bucket");

        _indexCount = NetBitConverter.ToInt32(bb, 8);
        _indexStart = 12;

        if (Source is FileBucket fb)
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
            _length = await Source.ReadRemainingBytesAsync().ConfigureAwait(false) + Source.Position ?? -1;
        }

        if (_preLoadExtensions)
        {
            await ProcessExtensionsAsync().ConfigureAwait(false);
            await Source.SeekAsync(_indexStart).ConfigureAwait(false);
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

    private async ValueTask<GitDirectoryEntry?> ReadEntryDirectAsync(bool allowSplit = false)
    {
        if (_indexStart == 0)
            await ReadHeaderAsync().ConfigureAwait(false);

        while (_skip > 0)
        {
            int n = await Source.ReadSkipAsync(_skip).ConfigureAwait(false);

            if (n == 0)
                throw new BucketEofException(Source);
            _skip -= n;
        }

        if (_nRead >= _indexCount)
        {
            if (!_endOfIndex.HasValue)
                _endOfIndex = Source.Position;

            if (!_firstReal.HasValue)
            {
                _firstReal = Source.Position;
                _firstRealIdx = _indexCount;
            }

            await ProcessExtensionsAsync().ConfigureAwait(false);

            return null;
        }
        _nRead++;

        int hashLen = _idType.HashLength();
        int readLen = 40 + 2 + hashLen;
        var bb = await Source.ReadAtLeastAsync(readLen).ConfigureAwait(false);

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
            _firstReal = Source.Position - bb.Length;
            _firstRealIdx = _nRead - 1;
        }

        int FullFlags = src.Flags;
        if ((src.Flags & 0x4000) != 0 && _version >= 3) // Must be 0 in version 2
        {
            bb = await Source.ReadAtLeastAsync(2).ConfigureAwait(false);

            FullFlags |= NetBitConverter.ToInt16(bb, 0) << 16;
        }

        if (_version < 4)
        {
            var (name, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.Zero).ConfigureAwait(false);

            if (eol != BucketEol.Zero)
                throw new BucketEofException(Source);

            _skip = 8 - (int)((Source.Position!.Value - _indexStart) & 0x7);

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
            int drop = (int)await Source.ReadGitDeltaOffsetAsync().ConfigureAwait(false);
            var (name, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.Zero).ConfigureAwait(false);

            if (eol != BucketEol.Zero)
                throw new BucketEofException(Source);

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
        await Source.SeekAsync(_indexStart).ConfigureAwait(false);

        return await ReadEntryAsync().ConfigureAwait(false);
    }

#pragma warning disable CA2213 // Disposable fields should be disposed
    private IAsyncEnumerator<bool>? _deletedWalk;
    private IAsyncEnumerator<bool>? _replacedWalk;
#pragma warning restore CA2213 // Disposable fields should be disposed
    private async ValueTask<GitDirectoryEntry?> ReadEntryLinkAsync()
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
                var me = await ReadEntryDirectAsync(allowSplit: true).ConfigureAwait(false);

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

    private GitDirectoryBucket? _remaining;
    private GitDirectoryEntry? _linkEntry;
    private GitDirectoryEntry? _remainingEntry;

    private async ValueTask<GitDirectoryEntry?> ReadMixedAsync()
    {
        if (_remaining is null && _firstReal.HasValue)
        {
            Bucket b = Source.Duplicate();
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

        if (_nRead < _indexCount || !_endOfIndex.HasValue)
        {
            while (null != await ReadEntryDirectAsync(allowSplit: true).ConfigureAwait(false))
            {
            }
        }

        if (!_endOfIndex.HasValue)
            throw new InvalidOperationException("EndOfIndex not set at end of reading");

        _processedExtensions = true;

        Bucket reader;
        if (_nRead == _indexCount)
            reader = Source.NoDispose(alwaysWrap: true);
        else
        {
            reader = await Source.DuplicateSeekedAsync(_endOfIndex.Value).ConfigureAwait(false);
        }

        await using (reader)
        {
            long bucketEnd = _length - _idType.HashLength();

            while (reader.Position < bucketEnd)
            {
                var bb = await reader.ReadAtLeastAsync(4 + 4).ConfigureAwait(false);

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
                        throw new BucketEofException($"Unexpected EOF in '{extensionName}' extension of {Name} bucket");
                }
            }
        }
    }

    private async ValueTask LoadLinkAsync(Bucket reader, int extensionLength)
    {
        long endPos = reader.Position!.Value + extensionLength;

        GitId shared = await reader.ReadGitIdAsync(_idType).ConfigureAwait(false);

        _deleted = new GitEwahBitmapBucket(reader.Duplicate());
        int delLength = await _deleted.ReadLengthAsync().ConfigureAwait(false);

        if (await reader.ReadSkipAsync(delLength).ConfigureAwait(false) != delLength)
            throw new BucketEofException(reader);

        _replaced = new GitEwahBitmapBucket(reader.Duplicate());
#if DEBUG
        int replaceLength = await _replaced.ReadLengthAsync().ConfigureAwait(false);

        Debug.Assert(delLength + replaceLength + _idType.HashLength() == extensionLength);
#endif
        await reader.SeekAsync(endPos).ConfigureAwait(false);

        if (!shared.IsZero)
            _shared = new GitDirectoryBucket(FileBucket.OpenRead(Path.Combine(_directory!, $"sharedindex.{shared}")), new GitDirectoryOptions() { IdType = _idType, LookForEndOfIndex = _lookForEndOfIndex });


        if (!_firstReal.HasValue)
        {
            while (await ReadEntryDirectAsync(allowSplit: true).ConfigureAwait(false) is GitDirectoryEntry entry)
            {
                if (!string.IsNullOrEmpty(entry.Name))
                    break;
            }

            Debug.Assert(_firstReal.HasValue);
        }
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        await ProcessExtensionsAsync().ConfigureAwait(false);

        while (0 != await Source.ReadSkipAsync(MaxRead).ConfigureAwait(false))
        {
        }

        return BucketBytes.Eof;
    }
}
