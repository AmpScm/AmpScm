using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git
{
    public sealed class GitPackFrameBucket : GitObjectBucket, IBucketPoll, IBucketSeek
    {
        Bucket? reader;
        frame_state state;
        long body_size;
        long position;
        long frame_position;
        long delta_position;
        readonly GitIdType _idType;
        Func<GitId, ValueTask<GitObjectBucket?>>? _fetchBucketById;
        GitId? _deltaId;

        enum frame_state
        {
            start,
            size_done,
            type_done,
            find_delta,
            open_body,
            body
        }

        public int? DeltaCount { get; private set; }
        public long? BodySize { get; private set; }

        public override string Name => (reader != null) ? $"GitPackFrame[{reader.Name}]>{Inner.Name}" : base.Name;

        // These types are in pack files, but not real objects
        const GitObjectType GitObjectType_DeltaOffset = (GitObjectType)6;
        const GitObjectType GitObjectType_DeltaReference = (GitObjectType)7;

        public GitPackFrameBucket(Bucket inner, GitIdType idType, Func<GitId, ValueTask<GitObjectBucket?>>? fetchBucketById = null)
            : base(inner.WithPosition())
        {
            _idType = idType;
            _fetchBucketById = fetchBucketById;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                reader?.Dispose();
            }
            finally
            {
                reader = null;
                base.Dispose(disposing);
            }
        }

        public override BucketBytes Peek()
        {
            if (reader == null || state != frame_state.body)
                return BucketBytes.Empty;

            return reader.Peek();
        }

        async ValueTask<BucketBytes> IBucketPoll.PollAsync(int minRequested /*= 1*/)
        {
            if (reader == null || state != frame_state.body)
                return BucketBytes.Empty;

            return await reader.PollAsync(minRequested).ConfigureAwait(false);
        }

        async ValueTask IBucketSeek.SeekAsync(long newPosition)
        {
            if (newPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(newPosition));

            if (reader == null || state != frame_state.body)
            {
                // Not reading yet. Just skip
                long np = await ReadSkipAsync(newPosition).ConfigureAwait(false);

                if (np != newPosition)
                    throw new GitBucketException($"Unable to seek to position {newPosition} on {Name}");
            }
            else
            {
                await reader.SeekAsync(newPosition).ConfigureAwait(false);
            }
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (reader == null || state != frame_state.body)
            {
                if (!await ReadInfoAsync().ConfigureAwait(false))
                    return BucketBytes.Eof;
            }

            return await reader!.ReadAsync(requested).ConfigureAwait(false);
        }

        public override async ValueTask<long> ReadSkipAsync(long requested)
        {
            if (reader == null || state != frame_state.body)
            {
                if (!await ReadInfoAsync().ConfigureAwait(false))
                    return 0;
            }

            return await reader!.ReadSkipAsync(requested).ConfigureAwait(false);
        }

        public override async ValueTask ReadTypeAsync()
        {
            await PrepareState(frame_state.type_done).ConfigureAwait(false);

            Debug.Assert(Type >= GitObjectType.Commit && Type <= GitObjectType.Tag);
        }

        public ValueTask<bool> ReadInfoAsync()
        {
            return PrepareState(frame_state.body);
        }

        async ValueTask<bool> PrepareState(frame_state want_state)
        {
            if (state >= frame_state.body)
                return true;

            const long max_size_len = 1 + (64 - 4 + 6) / 7;

            while (state == frame_state.start)
            {
                // In the initial state we use position to keep track of our
                // location withing the compressed length

                var peeked = await Inner.PollAsync().ConfigureAwait(false);

                int rq_len;

                if (!peeked.IsEmpty)
                {
                    rq_len = 0;
                    for (int i = 0; i <= max_size_len && i < peeked.Length; i++)
                    {
                        rq_len++;
                        if (0 == (peeked[i] & 0x80))
                            break;
                    }
                    rq_len = Math.Min(rq_len, peeked.Length);
                }
                else
                    rq_len = 1;

                var read = await Inner.ReadAsync(rq_len).ConfigureAwait(false);

                for (int i = 0; i < read.Length; i++)
                {
                    byte uc = read[i];

                    if (position == 0)
                    {
                        Type = (GitObjectType)((uc >> 4) & 0x7);
                        body_size = uc & 0xF;

                        long my_offs = Inner.Position!.Value;
                        if (my_offs >= 0)
                            frame_position = my_offs - read.Length;
                    }
                    else
                        body_size |= (long)(uc & 0x7F) << (4 + 7 * ((int)position - 1));

                    if (0 == (uc & 0x80))
                    {
                        if (position > max_size_len)
                            throw new GitBucketException("Git pack framesize overflows int64");

                        if (Type == GitObjectType.None)
                            throw new GitBucketException("Git pack frame 0 is invalid");
                        else if ((int)Type == 5)
                            throw new GitBucketException("Git pack frame 5 is unsupported");

                        Debug.Assert(i == read.Length - 1);
                        state = frame_state.size_done;
                        position = 0;
                        BodySize = body_size;
                    }
                    else
                        position++;
                }
            }

            if (want_state == frame_state.type_done && state == frame_state.type_done)
                return true;

            while (state <= frame_state.size_done)
            {
                if (Type == GitObjectType_DeltaReference)
                {
                    position = 0;

                    int hashLen = _idType.HashLength();
                    var read = await Inner.ReadFullAsync(hashLen).ConfigureAwait(false);

                    if (read.Length != hashLen)
                        throw new GitBucketEofException($"Unexpected EOF in {Name} bucket");

                    _deltaId = new GitId(_idType, read.ToArray());

                    state = frame_state.find_delta;
                }
                else if (Type == GitObjectType_DeltaOffset)
                {
                    // Body starts with negative offset of the delta base.
                    delta_position = await Inner.ReadGitOffsetAsync().ConfigureAwait(false);

                    if (delta_position > frame_position)
                        throw new GitBucketException("Delta position must point to earlier object in file");

                    state = frame_state.find_delta;
                    position = 0;
                    delta_position = frame_position - delta_position;
                }
                else
                {
                    position = 0; // The real body starts right now
                    state = frame_state.open_body;
                    DeltaCount = 0;
                    _fetchBucketById = null;
                }
            }

            if (state == frame_state.find_delta)
            {
                GitObjectBucket base_reader;

                if (Type == GitObjectType_DeltaOffset)
                {
                    // TODO: This is not restartable via async handling, while it should be.

                    // The source needs support for this. Our file and memory buckets have this support
                    Bucket deltaSource = await Inner.DuplicateAsync(true).ConfigureAwait(false);
                    await deltaSource.SeekAsync(delta_position).ConfigureAwait(false);

                    base_reader = new GitPackFrameBucket(deltaSource, _idType, _fetchBucketById);
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

                await base_reader.ReadTypeAsync().ConfigureAwait(false);
                Type = base_reader.Type;

                if (base_reader is GitPackFrameBucket fb)
                    DeltaCount = fb.DeltaCount + 1;
                else
                    DeltaCount = 1;

                reader = base_reader;
                _fetchBucketById = null;
                state = frame_state.open_body;
            }

            if (want_state == frame_state.type_done && state >= frame_state.type_done)
                return true;

            if (state == frame_state.open_body)
            {
                var inner = new ZLibBucket(Inner.SeekOnReset().NoClose(), BucketCompressionAlgorithm.ZLib);
                if (DeltaCount != 0)
                    reader = new GitDeltaBucket(inner, reader!);
                else
                    reader = inner;

                state = frame_state.body;
            }

            return true;
        }

        public override long? Position => (state == frame_state.body) ? reader!.Position : 0;

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            if (state < frame_state.body)
            {
                var b = await ReadInfoAsync().ConfigureAwait(false);

                if (!b)
                    return null;
            }

            if (DeltaCount > 0)
                return await reader!.ReadRemainingBytesAsync().ConfigureAwait(false);
            else
                return body_size - reader!.Position;
        }

        public override async ValueTask ResetAsync()
        {
            if (state < frame_state.body)
                return; // Nothing to reset

            await reader!.ResetAsync().ConfigureAwait(false);
        }

        public override bool CanReset => Inner.CanReset;
    }
}
