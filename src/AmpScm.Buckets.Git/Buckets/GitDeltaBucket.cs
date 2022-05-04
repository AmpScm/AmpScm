using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git
{
    public sealed class GitDeltaBucket : GitObjectBucket, IBucketPoll, IBucketSeek
    {
        internal GitObjectBucket BaseBucket { get; }
        long _length;
        long _position;
        uint _copyOffset;
        int _copySize;
        delta_state state;

        enum delta_state
        {
            start,
            init,
            src_copy,
            base_seek,
            base_copy,
            eof
        }

        public GitDeltaBucket(Bucket source, GitObjectBucket baseBucket)
            : base(source)
        {
            BaseBucket = baseBucket ?? throw new ArgumentNullException(nameof(baseBucket));
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && !DontDisposeInner)
                {
                    BaseBucket.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public override long? Position => _position;

        static int PopCount(uint value)
        {
#if NETFRAMEWORK
            value -= ((value >> 1) & 0x55555555);
            value = (value & 0x33333333) + ((value >> 2) & 0x33333333);
            return (int)((((value + (value >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24);
#else
            return BitOperations.PopCount(value);
#endif
        }

        static int NeedBytes(byte data)
        {
            if (0 == (data & 0x80))
                return 1;
            else
                return PopCount(data);
        }

        async ValueTask<bool> AdvanceAsync()
        {
            if (state == delta_state.start)
            {
                await ReadRemainingBytesAsync().ConfigureAwait(false);
            }

            if (state == delta_state.init)
            {
                BucketBytes data;

                byte uc;

                data = await Inner.ReadAsync(1).ConfigureAwait(false);

                if (data.IsEof)
                    throw new GitBucketEofException(Inner);

                uc = data[0];

                if (0 == (uc & 0x80))
                {
                    state = delta_state.src_copy;
                    _copySize = (uc & 0x7F);

                    if (_copySize == 0)
                        throw new GitBucketException("0 operation is reserved");
                }
                else
                {
                    int want = NeedBytes(uc) - 1;
                    _copyOffset = 0;
                    _copySize = 0;

                    if (want > 0)
                    {
                        data = await Inner.ReadFullAsync(want).ConfigureAwait(false);

                        if (data.Length < want)
                            throw new GitBucketEofException(Inner);

                        int i = 0;

                        if (0 != (uc & 0x01))
                            _copyOffset |= (uint)data[i++] << 0;
                        if (0 != (uc & 0x02))
                            _copyOffset |= (uint)data[i++] << 8;
                        if (0 != (uc & 0x04))
                            _copyOffset |= (uint)data[i++] << 16;
                        if (0 != (uc & 0x08))
                            _copyOffset |= (uint)data[i++] << 24;

                        if (0 != (uc & 0x10))
                            _copySize |= data[i++] << 0;
                        if (0 != (uc & 0x20))
                            _copySize |= data[i++] << 8;
                        if (0 != (uc & 0x40))
                            _copySize |= data[i++] << 16;
                    }

                    if (_copySize == 0)
                        _copySize = 0x10000;

                    if (_copyOffset == BaseBucket.Position!.Value)
                        state = delta_state.base_copy;
                    else
                        state = delta_state.base_seek;
                }
            }
            return true;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (requested <= 0)
                throw new ArgumentOutOfRangeException(nameof(requested));

            if (state <= delta_state.init && !await AdvanceAsync().ConfigureAwait(false))
                return BucketBytes.Eof;

            Debug.Assert(state >= delta_state.src_copy && state <= delta_state.eof);

            if (state == delta_state.base_seek)
                await SeekBase().ConfigureAwait(false);

            if (state == delta_state.base_copy)
            {
                var data = await BaseBucket.ReadAsync(Math.Min(requested, _copySize)).ConfigureAwait(false);

                if (data.IsEof)
                    throw new GitBucketEofException($"Unexpected EOF on Base {BaseBucket.Name} Bucket");

                _position += data.Length;
                _copySize -= data.Length;

                if (_copySize == 0)
                {
                    if (_position == _length)
                        state = delta_state.eof;
                    else
                        state = delta_state.init;
                }
                return data;
            }
            else if (state == delta_state.src_copy)
            {
                var data = await Inner.ReadAsync(Math.Min(requested, _copySize)).ConfigureAwait(false);

                if (data.IsEof)
                    throw new GitBucketEofException($"Unexpected EOF on Source {Inner.Name} Bucket");

                _position += data.Length;
                _copySize -= data.Length;

                if (_copySize == 0)
                {
                    if (_position == _length)
                        state = delta_state.eof;
                    else
                        state = delta_state.init;
                }
                return data;
            }
            else if (state == delta_state.eof)
            {
                var data = await Inner.ReadAsync().ConfigureAwait(false); // Ensure finish bytes of ZLib bucket are read

                if (!data.IsEof)
                    throw new InvalidOperationException($"Expected EOF on Source {Inner.Name} Bucket");

                return BucketBytes.Eof;
            }

            throw new InvalidOperationException();
        }

        async ValueTask IBucketSeek.SeekAsync(long newPosition)
        {
            if (newPosition < 0)
                throw new ArgumentNullException(nameof(newPosition));

            long curPosition = Position!.Value;

            if (newPosition < curPosition)
            {
                await ResetAsync().ConfigureAwait(false);
                curPosition = 0;
            }

            while (curPosition < newPosition)
            {
                long skipped = await ReadSkipAsync(newPosition - curPosition).ConfigureAwait(false);
                if (skipped == 0)
                    throw new InvalidOperationException($"Unexpected seek failure on {Name} Bucket position {newPosition} from {curPosition}");

                curPosition += skipped;
            }
        }

        public override async ValueTask<long> ReadSkipAsync(long requested)
        {
            int skipped = 0;
            while (requested > 0)
            {
                if (state <= delta_state.init && !await AdvanceAsync().ConfigureAwait(false))
                    return skipped;

                Debug.Assert(state >= delta_state.src_copy && state <= delta_state.eof);

                int rq = (int)Math.Min(requested, int.MaxValue);
                int r;
                switch (state)
                {
                    case delta_state.base_seek:
                        // Avoid the seek and just do the calculations instead
                        r = Math.Min(rq, _copySize);
                        _copySize -= r;
                        _copyOffset += (uint)r;
                        _position += r;
                        break;
                    case delta_state.base_copy:
                        // Go back to a to-seek state
                        r = Math.Min(rq, _copySize);
                        _copySize -= r;
                        _copyOffset = (uint)(BaseBucket.Position!.Value + r);
                        _position += r;
                        state = delta_state.base_seek;
                        break;
                    case delta_state.src_copy:
                        // Just skip the source data
                        r = (int)await Inner.ReadSkipAsync(Math.Min(rq, _copySize)).ConfigureAwait(false);
                        _copySize -= r;
                        _position += r;
                        break;
                    case delta_state.eof:
                        return 0;
                    default:
                        throw new InvalidOperationException();
                }

                if (_copySize == 0)
                    state = delta_state.init;

                if (r == 0)
                    return skipped;

                skipped += r;
                requested -= r;
            }
            return skipped;
        }

        private async ValueTask SeekBase()
        {
            while (state == delta_state.base_seek)
            {
                await BaseBucket.SeekAsync(_copyOffset).ConfigureAwait(false);

                state = delta_state.base_copy;
                _copyOffset = 0;
            }
        }

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            if (state == delta_state.start)
            {
                var base_len = await Inner.ReadGitDeltaSize().ConfigureAwait(false);

                long? base_size = await BaseBucket.ReadRemainingBytesAsync().ConfigureAwait(false);

                if (base_size != base_len)
                    throw new GitBucketException($"Expected delta base size {_length} doesn't match source size ({base_size}) on {BaseBucket.Name} Bucket");

                _length = await Inner.ReadGitDeltaSize().ConfigureAwait(false);
                state = delta_state.init;
            }

            return (_length - _position);
        }

        public override BucketBytes Peek()
        {
            if (state == delta_state.base_copy)
            {
                var data = BaseBucket.Peek();

                if (_copySize < data.Length)
                    data = data.Slice(0, _copySize);

                return data;
            }
            else if (state == delta_state.src_copy)
            {
                var data = Inner.Peek();

                if (_copySize < data.Length)
                    data = data.Slice(0, _copySize);

                return data;
            }
            else
                return BucketBytes.Empty;
        }

        public async ValueTask<BucketBytes> PollAsync(int minRequested = 1)
        {
            if (state == delta_state.base_copy || state == delta_state.src_copy)
                return Peek();

            await AdvanceAsync().ConfigureAwait(false);

            if (state == delta_state.base_seek)
                await SeekBase().ConfigureAwait(false);

            return Peek();
        }

        public override bool CanReset => Inner.CanReset && BaseBucket.CanReset;

        public override string Name => "GitDelta[" + Inner.Name + "]>" + BaseBucket.Name;

        public override async ValueTask ResetAsync()
        {
            if (!CanReset)
                throw new InvalidOperationException($"Reset not supported on {Name} bucket");

            await Inner.ResetAsync().ConfigureAwait(false); // Default error text or source reset
            await BaseBucket.ResetAsync().ConfigureAwait(false);

            state = delta_state.start;
            _length = 0;
            _position = 0;
            _copyOffset = 0;
            _copySize = 0;
        }

        public override ValueTask<GitObjectType> ReadTypeAsync()
        {
            return BaseBucket.ReadTypeAsync();
        }
    }
}
