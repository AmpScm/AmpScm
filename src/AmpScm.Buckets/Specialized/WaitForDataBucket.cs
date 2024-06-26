﻿using System.Diagnostics;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized;

internal sealed class WaitForDataBucket : ProxyBucket<WaitForDataBucket>, IBucketAggregation, IBucketWriter
{
    private bool _waitingForMore;
    private bool _readEof;
    private TaskCompletionSource<bool>? _waiter;
    private readonly object _l = new();

    public WaitForDataBucket()
        : base(new AggregateBucket())
    {
        Aggregation = (AggregateBucket)Source;
    }

    private AggregateBucket Aggregation { get; }

    Bucket IBucketAggregation.Append(Bucket bucket)
    {
        Append(bucket);

        return this;
    }

    Bucket IBucketAggregation.Prepend(Bucket bucket)
    {
        Prepend(bucket);

        return this;
    }

    public void Append(Bucket bucket)
    {
        lock (_l)
        {

            var b = Aggregation.Append(bucket);

            Debug.Assert(b == Aggregation);

            _waiter?.TrySetResult(true);
        }
    }

    public void Prepend(Bucket bucket)
    {
        lock (_l)
        {

            var b = Aggregation.Prepend(bucket);

            Debug.Assert(b == Aggregation);

            _waiter?.TrySetResult(true);
        }
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        while (true)
        {
            var bb = await Aggregation.ReadCombinedAsync(4096).ConfigureAwait(false);
            lock (_l)
            {
                if (!bb.IsEof)
                    return bb;
                else if (_readEof)
                    return bb; // EOF

                if (!_waitingForMore)
                {
                    _waiter = new TaskCompletionSource<bool>();
                    _waitingForMore = true;
                }
#if DEBUG
                else
                    Debug.WriteLine("Waiting on waiter more than once");
#endif
            }
            await _waiter!.Task.ConfigureAwait(false);

            lock (_l)
            {
                _waitingForMore = false;
                _waiter = null;
            }
        }
    }

    public void Write(Bucket bucket)
    {
        Append(bucket);
    }

    public ValueTask ShutdownAsync()
    {
        _readEof = true;
        return default;
    }
}
