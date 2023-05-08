using System;
using System.Linq;
using System.Threading.Tasks;
using AmpScm.Buckets;

namespace AmpScm.Buckets.Specialized;

internal sealed class LeaveBucket : WrappingBucket
{
    private readonly Func<BucketBytes, long, ValueTask>? _leftHandler;
    private long? _totalSize;
    private bool _done;
    private byte[]? _bufferLeft;

    private long CurrentPosition { get; set; }

    public LeaveBucket(Bucket source, int leaveBytes, Func<BucketBytes, long, ValueTask>? leftHandler) : base(source)
    {
        if (leaveBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(leaveBytes));

        _leftHandler = leftHandler;
        LeaveBytes = leaveBytes;
    }

    private int LeaveBytes { get; }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        if (requested < 0) throw new ArgumentOutOfRangeException(nameof(requested), requested, message: null);

        if (_done)
            return BucketBytes.Eof;

        while (true)
        {
            BucketBytes bb;

            if (_totalSize == null)
            {
                long? r = await Source.ReadRemainingBytesAsync().ConfigureAwait(false);

                if (r.HasValue)
                    _totalSize = CurrentPosition + r.Value;
            }

            if (_totalSize > 0)
            {
                long? remaining = _totalSize - CurrentPosition - LeaveBytes;

                if (requested > remaining)
                    requested = (int)remaining;

                if (requested <= 0)
                {
                    _done = true;
                    var left = await Source.ReadExactlyAsync(LeaveBytes).ConfigureAwait(false);

                    if (_leftHandler is { })
                        await _leftHandler.Invoke(left, CurrentPosition).ConfigureAwait(false);

                    left = await Source.ReadAsync(1).ConfigureAwait(false);

                    if (!left.IsEmpty)
                        throw new BucketException($"{Source} bucket has data after the left block");

                    return BucketBytes.Eof;
                }

                if (_bufferLeft == null)
                {
                    bb = await Source.ReadAsync(requested).ConfigureAwait(false);
                    CurrentPosition += bb.Length;
                    return bb;
                }
            }

            if (_bufferLeft is null)
            {
                // Fill the buffer
                if (requested < MaxRead - LeaveBytes)
                    requested += LeaveBytes;

                var b = await Source.ReadAsync(requested).ConfigureAwait(false);

                if (b.Length > LeaveBytes)
                {
                    _bufferLeft = b.Slice(b.Length - LeaveBytes).ToArray();
                    CurrentPosition += b.Length - LeaveBytes;
                    return b.Slice(0, b.Length - LeaveBytes);
                }
                else if (b.Length > 1)
                {
                    _bufferLeft = b.ToArray();
                    continue; // And continue reading
                }
                else
                    return BucketBytes.Eof;
            }

            if (_bufferLeft.Length > LeaveBytes)
            {
                // We have data that we can just return
                int have = _bufferLeft.Length - LeaveBytes;

                int want = Math.Max(have, requested);

                byte[] all = _bufferLeft;
                _bufferLeft = _bufferLeft.Skip(want).ToArray();

                CurrentPosition += want;
                return new(all, 0, want);
            }

            // What have some data, but have to read more to know how much we can return
            if (_totalSize > 0)
            {
                byte[] tmp;
                // We now know how many data we can fetch... get rid of buffer and go optimized!
                if (requested >= _bufferLeft.Length)
                {
                    tmp = _bufferLeft;
                    _bufferLeft = null;
                    return tmp;
                }


                tmp = _bufferLeft.Take(requested).ToArray();
                _bufferLeft = _bufferLeft.Skip(requested).ToArray();
                return tmp;
            }

            if (requested > MaxRead - LeaveBytes)
                requested = MaxRead;

            requested += LeaveBytes - _bufferLeft.Length;

            bb = await Source.ReadAsync(requested).ConfigureAwait(false);

            if (bb.Length < LeaveBytes - _bufferLeft.Length)
            {
                // We didn't get enough bytes to swap buffers :(
                ByteCollector bc = new();
                bc.Append(_bufferLeft);
                bc.Append(bb);

                byte[] r = bc.ToArray();
                _bufferLeft = r.Skip(r.Length - LeaveBytes).ToArray();

                CurrentPosition += r.Length - LeaveBytes;
                return new(r, 0, r.Length - LeaveBytes);

            }
            else if (bb.Length + _bufferLeft.Length > LeaveBytes)
            {
                byte[] ret = _bufferLeft;
                _bufferLeft = bb.ToArray();

                CurrentPosition += ret.Length;
                return ret;
            }
            else if (bb.IsEof)
            {
                _done = true;
                if (_leftHandler is { })
                    await _leftHandler.Invoke(_bufferLeft, CurrentPosition).ConfigureAwait(false);

                return BucketBytes.Eof;
            }
            else
                throw new NotImplementedException("Unhandled case... how?");
        }
    }

    public override long? Position => CurrentPosition;

    public override BucketBytes Peek()
    {
        var bb = Source.Peek();

        if (bb.Length > LeaveBytes)
            return bb.Slice(0, bb.Length - LeaveBytes);

        return BucketBytes.Empty;
    }

    public override Bucket Duplicate(bool reset = false)
    {
        var i = Source.Duplicate(reset);

        return new LeaveBucket(i, LeaveBytes, (_, _) => new());
    }

    public override async ValueTask<long?> ReadRemainingBytesAsync()
    {
        long? r = await Source.ReadRemainingBytesAsync().ConfigureAwait(false);

        if (r is { } remaining)
            return remaining - LeaveBytes;
        else
            return null;
    }
}
