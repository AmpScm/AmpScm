using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Git
{
    public sealed class GitEwahBitmapBucket : GitBucket
    {
        enum ewah_state
        {
            init = 0,
            start,
            same,
            raw,
            footer,
            done
        }
        BucketBytes _readable;
        ewah_state _state;
        uint _repCount;
        int _rawCount;
        int _compressedSize;
        uint? _lengthBits;
        int _left;
        byte[] _buffer;
        int _wpos;
        bool _repBit;
        int _position;

        public GitEwahBitmapBucket(Bucket inner)
            : base(inner)
        {
            _state = ewah_state.init;
            _buffer = new byte[4096];
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            while (true)
            {
                BucketBytes bb;
                if (!_readable.IsEmpty)
                {
                    if (requested > _readable.Length)
                    {
                        bb = _readable;
                        _readable = BucketBytes.Empty;
                        _position += bb.Length;
                        return bb;
                    }

                    bb = _readable.Slice(0, requested);
                    _readable = _readable.Slice(requested);
                    _position += bb.Length;
                    return bb;
                }

                if (!await RefillAsync(true).ConfigureAwait(false))
                    return BucketBytes.Eof;
            }
        }

        public override BucketBytes Peek()
        {
            if (_readable.IsEmpty)
                return _readable;

            RefillAsync(false).AsTask().GetAwaiter().GetResult();

            return _readable;
        }

        public async ValueTask<int> ReadBitLengthAsync()
        {
            if (_lengthBits is null)
            {
                await RefillAsync(true).ConfigureAwait(false);
            }

            return (int)_lengthBits!.Value;
        }

        public async ValueTask<int> ReadLengthAsync()
        {
            if (_lengthBits is null)
            {
                await RefillAsync(true).ConfigureAwait(false);
            }

            return _compressedSize * sizeof(ulong) + (/* HEADER: */ 4 + 4) + (/* Trailer: */ 4);
        }

        private async ValueTask<bool> RefillAsync(bool allowWait)
        {
            if (_state <= ewah_state.start && !allowWait && Inner.Peek().IsEmpty)
                return false;

            if (_lengthBits is null)
            {
                var bb = await Inner.ReadFullAsync(4 + 4).ConfigureAwait(false);
                _lengthBits = NetBitConverter.ToUInt32(bb, 0);
                _compressedSize = NetBitConverter.ToInt32(bb, 4);

                _left = _compressedSize;
                _state = ewah_state.start;
            }

            int peekLength = Inner.Peek().Length / sizeof(ulong);
            _wpos = 0;

            switch (_state)
            {
                case ewah_state.start:
                    ulong curOp = await Inner.ReadNetworkUInt64Async().ConfigureAwait(false);

                    _repBit = (curOp & 1UL) != 0;
                    _repCount = (uint)(curOp >> 1);
                    _rawCount = (int)(curOp >> 33);

                    _left--;
                    peekLength--;
                    _state = ewah_state.same;
                    goto case ewah_state.same;

                case ewah_state.same:
                    byte val = _repBit ? (byte)0xFF : (byte)0;
                    while (_repCount > 0 && _wpos + 8 < _buffer.Length)
                    {
                        _buffer[_wpos++] = val;
                        _buffer[_wpos++] = val;
                        _buffer[_wpos++] = val;
                        _buffer[_wpos++] = val;
                        _buffer[_wpos++] = val;
                        _buffer[_wpos++] = val;
                        _buffer[_wpos++] = val;
                        _buffer[_wpos++] = val;
                        _repCount--;
                    }
                    if (_repCount > 0)
                    {
                        _readable = new BucketBytes(_buffer, 0, _wpos);
                        return true;
                    }

                    _state = ewah_state.raw;
                    goto case ewah_state.raw;

                case ewah_state.raw:
                    while (_rawCount > 0)
                    {
                        if ((_wpos > 8 && peekLength < 8) || (_wpos + 8 >= _buffer.Length))
                        {
                            // Avoid new reads if we already have something. Return result
                            _readable = new BucketBytes(_buffer, 0, _wpos);
                            return true;
                        }

                        var bb = await Inner.ReadFullAsync(sizeof(ulong)).ConfigureAwait(false);

                        if (bb.Length != sizeof(ulong))
                            throw new BucketException("Unexpected EOF");

                        peekLength--;
                        _left--;
                        _rawCount--;

                        for (int i = bb.Length - 1; i >= 0; i--)
                        {
                            _buffer[_wpos++] = bb[i];
                        }
                    }

                    if (_left == 0)
                    {
                        _state = ewah_state.footer;
                        _readable = new BucketBytes(_buffer, 0, _wpos);
                        return true;
                    }

                    _state = ewah_state.start;
                    goto case ewah_state.start;
                case ewah_state.footer:
                    await Inner.ReadNetworkUInt32Async().ConfigureAwait(false);
                    _state = ewah_state.done;
                    goto case ewah_state.done;
                case ewah_state.done:
                default:
                    return false;
            }
        }

        public override bool CanReset => Inner.CanReset;

        public override async ValueTask ResetAsync()
        {
            await Inner.ResetAsync().ConfigureAwait(false);
            _state = ewah_state.init;
            _wpos = 0;
            _position = 0;
        }

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            if (_lengthBits is null)
                await RefillAsync(true).ConfigureAwait(false);

            return ((_lengthBits + 8 * sizeof(ulong) - 1) / (8 * sizeof(ulong))) * 8 - _position;
        }

        public override long? Position => _position;


        public IAsyncEnumerable<bool> AllBits => new EwahWalker(this);

        public IAsyncEnumerable<int> SetIndexes => new EwahWalker(this);


        sealed class EwahWalker : IAsyncEnumerable<bool>, IAsyncEnumerable<int>
        {
            private GitEwahBitmapBucket _bucket;

            public EwahWalker(GitEwahBitmapBucket bucket)
            {
                _bucket = bucket;
            }

            async IAsyncEnumerator<bool> IAsyncEnumerable<bool>.GetAsyncEnumerator(CancellationToken cancellationToken)
            {
                int bitLength = await _bucket.ReadBitLengthAsync().ConfigureAwait(false);
                int bit = 0;

                while (await _bucket.ReadByteAsync().ConfigureAwait(false) is byte b)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    for (int n = 0; n < 8; n++)
                    {
                        yield return (bit + n < bitLength) && ((b & (1 << n)) != 0);
                    }
                    bit += 8;
                }
            }

            async IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(CancellationToken cancellationToken)
            {
                int bitLength = await _bucket.ReadBitLengthAsync().ConfigureAwait(false);
                int bit = 0;

                while (await _bucket.ReadByteAsync().ConfigureAwait(false) is byte b)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    for (int n = 0; n < 8; n++)
                    {
                        if ((bit + n < bitLength) && ((b & (1 << n)) != 0))
                        {
                            yield return bit + n;
                        }
                    }
                    bit += 8;
                }
            }
        }
    }
}
