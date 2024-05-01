using System.Diagnostics;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git;

public sealed class GitPackObjectBucket : GitObjectBucket, IBucketPoll, IBucketSeek
{
#pragma warning disable CA2213 // Disposable fields should be disposed // Bad diagnostic
    private Bucket? _reader;
#pragma warning restore CA2213 // Disposable fields should be disposed
    private frame_state state;
    private long delta_position;
    private readonly GitIdType _idType;
    private Func<GitId, ValueTask<GitObjectBucket?>>? _fetchBucketById;
    private Func<long, ValueTask<GitObjectBucket>>? _fetchBucketByOffset;
    private GitId? _deltaId;
    private int? _deltaCount;
    private GitObjectType _type;

    private enum frame_state
    {
        start,
        size_done,
        type_done,
        find_delta,
        open_body,
        body
    }

    /// <summary>
    /// Gets the number of uncompressed bytes stored in this frame. Available when <see cref="ReadRemainingBytesAsync"/> is
    /// available, but doesn't look at the delta (if any), but only the data in this frame.
    /// </summary>
    public long? BodySize { get; private set; }

    public override string Name => (_reader != null) ? $"GitPackFrame[{_reader.Name}]>{Source.Name}" : base.Name;

    // These types are in pack files, but not real objects
    private const GitObjectType GitObjectType_DeltaOffset = (GitObjectType)6;
    private const GitObjectType GitObjectType_DeltaReference = (GitObjectType)7;

    public GitPackObjectBucket(Bucket source, GitIdType idType, Func<GitId, ValueTask<GitObjectBucket?>>? fetchBucketById = null, Func<long, ValueTask<GitObjectBucket>>? fetchBucketByOffset = null)
        : base(source.WithPosition())
    {
        _idType = idType;
        _fetchBucketById = fetchBucketById;
        _fetchBucketByOffset = fetchBucketByOffset;
    }

    protected override void Dispose(bool disposing)
    {
        if (_reader != null)
            _reader.Dispose();
        else
            base.Dispose(disposing);
    }

    public override BucketBytes Peek()
    {
        if (_reader == null || state != frame_state.body)
            return BucketBytes.Empty;

        return _reader.Peek();
    }

    async ValueTask<BucketBytes> IBucketPoll.PollAsync(int minRequested /*= 1*/)
    {
        if (_reader == null || state != frame_state.body)
            return BucketBytes.Empty;

        return await _reader.PollAsync(minRequested).ConfigureAwait(false);
    }

    public override async ValueTask SeekAsync(long newPosition)
    {
        if (newPosition < 0)
            throw new ArgumentOutOfRangeException(nameof(newPosition), newPosition, message: null);

        if (state != frame_state.body)
            await PrepareState(frame_state.body).ConfigureAwait(false);

        if (_reader is IBucketSeek bs)
            await bs.SeekAsync(newPosition).ConfigureAwait(false);
        else
            await _reader!.SeekAsync(newPosition).ConfigureAwait(false);
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        if (_reader == null || state != frame_state.body)
        {
            await ReadInfoAsync().ConfigureAwait(false);
        }

        return await _reader!.ReadAsync(requested).ConfigureAwait(false);
    }

    public override async ValueTask<long> ReadSkipAsync(long requested)
    {
        await ReadInfoAsync().ConfigureAwait(false);

        return await _reader!.ReadSkipAsync(requested).ConfigureAwait(false);
    }

    public override async ValueTask<GitObjectType> ReadTypeAsync()
    {
        await PrepareState(frame_state.type_done).ConfigureAwait(false);

        if ((_type == GitObjectType.None || _type > GitObjectType.Tag)
            && _reader is GitObjectBucket gob)
        {
            return _type = await gob.ReadTypeAsync().ConfigureAwait(false);
        }

        Debug.Assert(_type >= GitObjectType.Commit && _type <= GitObjectType.Tag, "Bad Git Type");
        return _type;
    }

    internal ValueTask ReadInfoAsync()
    {
        return PrepareState(frame_state.body);
    }

    internal async ValueTask<bool> ReadNeedsBaseAsync()
    {
        await PrepareState(frame_state.size_done).ConfigureAwait(false);

        return (_deltaCount != 0);
    }

#pragma warning disable MA0051 // Method is too long
    private async ValueTask PrepareState(frame_state want_state)
#pragma warning restore MA0051 // Method is too long
    {
        if (state >= frame_state.body)
            return;

        if (state == frame_state.start)
        {
            delta_position = Source.Position ?? 0;
            (_type, BodySize) = await ReadTypeAndSize().ConfigureAwait(false);
            state = frame_state.size_done;
        }

        if (want_state == frame_state.type_done && state == frame_state.type_done)
            return;

        if (state <= frame_state.size_done)
        {
            if (_type == GitObjectType_DeltaReference)
            {
                _deltaId = await Source.ReadGitIdAsync(_idType).ConfigureAwait(false);

                state = frame_state.find_delta;
                _deltaCount = -1;
            }
            else if (_type == GitObjectType_DeltaOffset)
            {
                // Body starts with negative offset of the delta base.
                var dp = await Source.ReadGitDeltaOffsetAsync().ConfigureAwait(false);

                if (dp > delta_position)
                    throw new GitBucketException($"Delta position must point to earlier object in {Name} Bucket");

                state = frame_state.find_delta;
                delta_position -= dp;
                _deltaCount = -1;
            }
            else
            {
                state = frame_state.open_body;
                _deltaCount = 0;
                _fetchBucketById = null;
            }

            if (want_state == frame_state.size_done)
                return;
        }

        if (state == frame_state.find_delta)
        {
            GitObjectBucket base_reader;

            if (_type == GitObjectType_DeltaOffset)
            {
                // The source needs support for this. Our file and memory buckets have this support
                if (_fetchBucketByOffset != null)
                    base_reader = await _fetchBucketByOffset(delta_position).ConfigureAwait(false);
                else
                {
                    Bucket deltaSource = await Source.DuplicateSeekedAsync(delta_position).ConfigureAwait(false);

                    base_reader = new GitPackObjectBucket(deltaSource, _idType, _fetchBucketById);
                }
            }
            else
            {
                if (_fetchBucketById == null)
                    throw new GitBucketException($"Found delta against {_deltaId!}, but don't have a resolver to obtain that object in {Name} Bucket");

                base_reader = (await _fetchBucketById(_deltaId!).ConfigureAwait(false))!;

                if (base_reader == null)
                    throw new GitBucketException($"Can't obtain delta-base bucket for {_deltaId!} in {Name} Bucket");
                _deltaId = null; // Not used any more
            }

            _reader = base_reader;
            _fetchBucketById = null;
            state = frame_state.open_body;
        }

        if (want_state == frame_state.type_done && state >= frame_state.type_done)
            return;

        if (state == frame_state.open_body)
        {
            int bufferSize;

            if (BodySize <= 65536)
                bufferSize = (int)BodySize;
            else
                bufferSize = ZLibBucket.DefaultBufferSize;

            var inner = new ZLibBucket(Source.SeekOnReset(), BucketCompressionAlgorithm.ZLib, bufferSize: bufferSize);
            if (_deltaCount != 0)
                _reader = new GitDeltaBucket(inner, (GitObjectBucket)_reader!);
            else
                _reader = inner;

            state = frame_state.body;
        }
    }

    private async ValueTask<(GitObjectType Type, long BodySize)> ReadTypeAndSize()
    {
        const long max_size_len = 1 + (64 - 4 + 6) / 7;
        long body_size = 0;
        GitObjectType type = GitObjectType.None;

        for (int i = 0; i <= max_size_len; i++)
        {
            byte uc = await Source.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(Source);

            if (i == 0)
            {
                type = (GitObjectType)((uc >> 4) & 0x7);
                body_size = uc & 0xF;
            }
            else
                body_size |= (long)(uc & 0x7F) << (4 + 7 * (i - 1));

            if (0 == (uc & 0x80))
            {
                if (type == GitObjectType.None)
                    throw new GitBucketException("Git pack frame 0 is invalid");
                else if ((int)type == 5)
                    throw new GitBucketException("Git pack frame 5 is unsupported");

                return (type, body_size);
            }
        }

        throw new GitBucketException($"Git pack framesize overflows int64 in {Name} Bucket");
    }


    public override long? Position => (state == frame_state.body) ? _reader!.Position : 0;

    public override async ValueTask<long?> ReadRemainingBytesAsync()
    {
        if (state < frame_state.body)
        {
            await ReadInfoAsync().ConfigureAwait(false);
        }

        if (_deltaCount != 0)
            return await _reader!.ReadRemainingBytesAsync().ConfigureAwait(false);
        else
            return BodySize - _reader!.Position;
    }

    public async ValueTask<int> ReadDeltaCountAsync()
    {
        await ReadInfoAsync().ConfigureAwait(false);

        if (_deltaCount >= 0)
            return _deltaCount.Value;

        if (_reader is GitDeltaBucket gdb && gdb.BaseBucket is GitPackObjectBucket fb)
        {
            var count = await fb.ReadDeltaCountAsync().ConfigureAwait(false) + 1;
            _deltaCount = count;
            return count;
        }

        _deltaCount = 0;
        return 0;
    }

    public override void Reset()
    {
        if (state < frame_state.body)
            return; // Nothing to reset

        _reader!.Reset();
    }

    public override bool CanReset => _reader?.CanReset ?? Source.CanReset;
}
