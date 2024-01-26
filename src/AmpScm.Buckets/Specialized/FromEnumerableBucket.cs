using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized;

internal sealed class FromEnumerableBucket : Bucket
{
    private readonly IAsyncEnumerable<BucketBytes> _enumerable;
    private IAsyncEnumerator<BucketBytes>? _enumerator;
    private BucketBytes? _next;

    public FromEnumerableBucket(IAsyncEnumerable<BucketBytes> enumerable)
    {
        if (enumerable is null)
            throw new ArgumentNullException(nameof(enumerable));

        _enumerable = enumerable;
        _enumerator = enumerable.GetAsyncEnumerator();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && _enumerator is not null)
        {
            _enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        _enumerator = null;
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        BucketBytes bb;
        if (_next.HasValue)
        {
            bb = _next.Value;

            if (bb.IsEof)
                return bb;

            _next = null;
            int n = Math.Min(bb.Length, requested);

            if (n >= bb.Length)
                return bb;
            else
            {
                _next = bb.Slice(n);
                return bb.Slice(0, n);
            }
        }

        if (_enumerator is null || !await _enumerator.MoveNextAsync().ConfigureAwait(false))
        {
            _next = BucketBytes.Eof;
            return BucketBytes.Eof;
        }

        bb = _enumerator.Current;

        if (bb.IsEof)
        {
            await _enumerator.DisposeAsync().ConfigureAwait(false);
            _enumerator = null;
            _next = bb;
            return bb;
        }

        {
            int n = Math.Min(bb.Length, requested);

            if (n >= bb.Length)
                return bb;
            else
            {
                _next = bb.Slice(n);
                return bb.Slice(0, n);
            }
        }
    }

    public override BucketBytes Peek()
    {
        return _next ?? base.Peek();
    }

    public override bool CanReset => false;
}
