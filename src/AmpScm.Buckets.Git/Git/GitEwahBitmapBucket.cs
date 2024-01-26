using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Git;

public sealed class GitEwahBitmapBucket : GitBucket
{
    private enum ewah_state
    {
        init = 0,
        start,
        same,
        raw,
        footer,
        done
    }

    private BucketBytes _readable;
    private ewah_state _state;
    private uint _repCount;
    private int _rawCount;
    private int _compressedSize;
    private uint? _lengthBits;
    private int _left;
    private byte[] _buffer;
    private int _wpos;
    private bool _repBit;
    private int _position;

    public GitEwahBitmapBucket(Bucket source)
        : base(source)
    {
        _state = ewah_state.init;
        _buffer = new byte[512];
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
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

            if (!await RefillAsync(allowWait: true).ConfigureAwait(false))
                return BucketBytes.Eof;
        }
    }

    public override BucketBytes Peek()
    {
        if (_readable.IsEmpty)
            return _readable;

        RefillAsync(allowWait: false).AsTask().Wait();

        return _readable;
    }

    public async ValueTask<int> ReadBitLengthAsync()
    {
        if (_lengthBits is null)
        {
            await RefillAsync(allowWait: true).ConfigureAwait(false);
        }

        return (int)_lengthBits!.Value;
    }

    public async ValueTask<int> ReadLengthAsync()
    {
        if (_lengthBits is null)
        {
            await RefillAsync(allowWait: true).ConfigureAwait(false);
        }

        return _compressedSize * sizeof(ulong) + (/* HEADER: */ 4 + 4) + (/* Trailer: */ 4);
    }

#pragma warning disable MA0051 // Method is too long
    private async ValueTask<bool> RefillAsync(bool allowWait)
#pragma warning restore MA0051 // Method is too long
    {
        if (_state <= ewah_state.start && !allowWait && Source.Peek().IsEmpty)
            return false;

        if (_lengthBits is null)
        {
            var bb = await Source.ReadExactlyAsync(4 + 4).ConfigureAwait(false);
            _lengthBits = NetBitConverter.ToUInt32(bb, 0);
            _compressedSize = NetBitConverter.ToInt32(bb, 4);

            _left = _compressedSize;
            _state = ewah_state.start;
        }

        int peekLength = Source.Peek().Length / sizeof(ulong);
        _wpos = 0;

        switch (_state)
        {
            case ewah_state.start:
                ulong curOp = await Source.ReadNetworkUInt64Async().ConfigureAwait(false);

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

                    var bb = await Source.ReadExactlyAsync(sizeof(ulong)).ConfigureAwait(false);

                    if (bb.Length != sizeof(ulong))
                        throw new BucketEofException(Source);

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
                await Source.ReadNetworkUInt32Async().ConfigureAwait(false);
                _state = ewah_state.done;
                goto case ewah_state.done;
            case ewah_state.done:
            default:
                return false;
        }
    }

    public override bool CanReset => Source.CanReset;

    public override void Reset()
    {
        Source.Reset();
        _state = ewah_state.init;
        _wpos = 0;
        _position = 0;
    }

    public override async ValueTask<long?> ReadRemainingBytesAsync()
    {
        if (_lengthBits is null)
            await RefillAsync(allowWait: true).ConfigureAwait(false);

        return ((_lengthBits + 8 * sizeof(ulong) - 1) / (8 * sizeof(ulong))) * 8 - _position;
    }

    public override long? Position => _position;


    public IAsyncEnumerable<bool> AllBits => new EwahWalker(this);

    public IAsyncEnumerable<int> SetIndexes => new EwahWalker(this);

    private sealed class EwahWalker : IAsyncEnumerable<bool>, IAsyncEnumerable<int>
    {
        private GitEwahBitmapBucket _bucket;

        public EwahWalker(GitEwahBitmapBucket bucket)
        {
            _bucket = bucket;
        }

        async IAsyncEnumerator<bool> IAsyncEnumerable<bool>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            int bitLength = await _bucket.ReadBitLengthAsync().ConfigureAwait(false);
            byte b = 0;

            for (int i = 0; i < bitLength; i++)
            {
                if ((i & 7) == 0)
                {
                    b = (await _bucket.ReadByteAsync().ConfigureAwait(false)).Value;
                }

                int n = i & 7;

                yield return ((b & (1 << n)) != 0);
            }
        }

        async IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            int bitLength = await _bucket.ReadBitLengthAsync().ConfigureAwait(false);
            byte b = 0;

            for (int i = 0; i < bitLength; i++)
            {
                if ((i & 7) == 0)
                {
                    b = (await _bucket.ReadByteAsync().ConfigureAwait(false)).Value;

                    if (b == 0)
                    {
                        i += 7;
                        continue;
                    }
                }

                int n = i & 7;

                if ((b & (1 << n)) != 0)
                    yield return i;
            }
        }
    }
}
