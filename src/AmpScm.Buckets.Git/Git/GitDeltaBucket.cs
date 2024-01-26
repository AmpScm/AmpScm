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
    internal sealed class GitDeltaBucket : GitObjectBucket, IBucketPoll, IBucketSeek
    {
        internal GitObjectBucket BaseBucket { get; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private long _length;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private long _position;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private uint _copyOffset;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _copySize;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _origCopySize;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private delta_state state;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private long _baseLen;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _openedBase;

        private enum delta_state
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
                BaseBucket.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public override long? Position => _position;

        private static int PopCount(uint value)
        {
#if NETFRAMEWORK
            value -= ((value >> 1) & 0x55555555);
            value = (value & 0x33333333) + ((value >> 2) & 0x33333333);
            return (int)((((value + (value >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24);
#else
            return BitOperations.PopCount(value);
#endif
        }

        private static int NeedBytes(byte data)
        {
            if (0 == (data & 0x80))
                return 1;
            else
                return PopCount(data);
        }

        private async ValueTask<bool> AdvanceAsync()
        {
            if (state == delta_state.start)
            {
                await ReadRemainingBytesAsync().ConfigureAwait(false);
            }

            if (_copySize == 0 && state >= delta_state.src_copy && state <= delta_state.base_copy)
                state = delta_state.init;

            if (state == delta_state.init)
            {
                BucketBytes data;

                byte uc;

                data = await Source.ReadAsync(1).ConfigureAwait(false);

                if (data.IsEof)
                {
                    if (_position != _length)
                        throw new BucketEofException(Source);
                    else
                    {
                        state = delta_state.eof;
                        return false;
                    }
                }

                uc = data[0];

                if (0 == (uc & 0x80))
                {
                    state = delta_state.src_copy;
                    _origCopySize = _copySize = (uc & 0x7F);

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
                        data = await Source.ReadExactlyAsync(want).ConfigureAwait(false);

                        if (data.Length < want)
                            throw new BucketEofException(Source);

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

                    _origCopySize = _copySize;

                    if (_copyOffset == BaseBucket.Position!.Value)
                        state = delta_state.base_copy;
                    else
                        state = delta_state.base_seek;
                }
            }
            return true;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            if (requested <= 0)
                throw new ArgumentOutOfRangeException(nameof(requested), requested, message: null);

            if (_copySize == 0 && !await AdvanceAsync().ConfigureAwait(false))
                return BucketBytes.Eof;

            Debug.Assert(state >= delta_state.src_copy && state <= delta_state.eof);

            if (state == delta_state.base_seek)
                await SeekBase().ConfigureAwait(false);

            if (state == delta_state.base_copy)
            {
                if (!_openedBase)
                {
                    _openedBase = true;
                    long? base_size = await BaseBucket.ReadRemainingBytesAsync().ConfigureAwait(false) + BaseBucket.Position;

                    if (base_size != _baseLen)
                        throw new GitBucketException($"Expected delta base size {_baseLen} doesn't match source size ({base_size}) on {BaseBucket.Name} Bucket");
                }

                var data = await BaseBucket.ReadAsync(Math.Min(requested, _copySize)).ConfigureAwait(false);

                if (data.IsEof)
                    throw new BucketEofException($"Unexpected EOF on Base {BaseBucket.Name} Bucket");

                _position += data.Length;
                _copySize -= data.Length;

                return data;
            }
            else if (state == delta_state.src_copy)
            {
                var data = await Source.ReadAsync(Math.Min(requested, _copySize)).ConfigureAwait(false);

                if (data.IsEof)
                    throw new BucketEofException($"Unexpected EOF on Source {Source.Name} Bucket");

                _position += data.Length;
                _copySize -= data.Length;

                return data;
            }
            else if (state == delta_state.eof)
            {
                var data = await Source.ReadAsync().ConfigureAwait(false); // Ensure finish bytes of ZLib bucket are read

                if (!data.IsEof)
                    throw new InvalidOperationException($"Expected EOF on Source {Source.Name} Bucket");

                return BucketBytes.Eof;
            }

            throw new InvalidOperationException();
        }

        public override async ValueTask SeekAsync(long newPosition)
        {
            if (newPosition < 0)
                throw new ArgumentNullException(nameof(newPosition));

            long wantReverse = _position - newPosition;

            if (wantReverse == 0)
                return;
            else if (wantReverse > 0)
            {
                if (state == delta_state.base_copy || state == delta_state.src_copy)
                {
                    // Are we lucky enough that we can just seek back in the current block?

                    if (wantReverse <= (_origCopySize - _copySize))
                    {
                        int rev = (int)wantReverse;

                        if (state == delta_state.base_copy)
                            await BaseBucket.SeekAsync(BaseBucket.Position!.Value - rev).ConfigureAwait(false);
                        else
                            await Source.SeekAsync(Source.Position!.Value - rev).ConfigureAwait(false);

                        _position -= rev;
                        _copySize += rev;
                        return;
                    }
                }

                Reset();
            }

            if (_position < newPosition)
            {
                await ReadSkipAsync(newPosition - _position).ConfigureAwait(false);
            }
        }

        public override async ValueTask<long> ReadSkipAsync(long requested)
        {
            long skipped = 0;
            while (requested > 0)
            {
                if (_copySize == 0 && !await AdvanceAsync().ConfigureAwait(false))
                {
                    return skipped;
                }

                Debug.Assert(state >= delta_state.src_copy && state <= delta_state.eof);

                int rq = (int)Math.Min(requested, Bucket.MaxRead);
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
                        r = (int)await Source.ReadSkipAsync(Math.Min(rq, _copySize)).ConfigureAwait(false);
                        _copySize -= r;
                        _position += r;
                        break;
                    case delta_state.eof:
                        return skipped;
                    default:
                        throw new InvalidOperationException();
                }

                if (r == 0)
                    return skipped;

                skipped += r;
                requested -= r;
            }
            return skipped;
        }

        private async ValueTask SeekBase()
        {
            if (state == delta_state.base_seek)
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
                _baseLen = await Source.ReadGitDeltaSize().ConfigureAwait(false);

                _length = await Source.ReadGitDeltaSize().ConfigureAwait(false);
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
                var data = Source.Peek();

                if (_copySize < data.Length)
                    data = data.Slice(0, _copySize);

                return data;
            }
            else
                return BucketBytes.Empty;
        }

        public async ValueTask<BucketBytes> PollAsync(int minRequested = 1)
        {
            if ((state == delta_state.base_copy || state == delta_state.src_copy) && _copySize > 0)
                return Peek();

            if (!await AdvanceAsync().ConfigureAwait(false))
                return BucketBytes.Eof;

            if (state == delta_state.base_seek)
                await SeekBase().ConfigureAwait(false);

            return Peek();
        }

        public override bool CanReset => Source.CanReset && BaseBucket.CanReset;

        public override string Name => "GitDelta[" + Source.Name + "]>" + BaseBucket.Name;

        public override void Reset()
        {
            if (!CanReset)
                throw new InvalidOperationException($"Reset not supported on {Name} bucket");

            Source.Reset(); // Will either throw error text or perform the source reset
            // No need to reset base. We seek on use anyway.

            state = delta_state.start;
            _position = 0;
            _copyOffset = 0;
            _origCopySize = _copySize = 0;
            _baseLen = 0;
        }

        public override ValueTask<GitObjectType> ReadTypeAsync()
        {
            return BaseBucket.ReadTypeAsync();
        }
    }
}
