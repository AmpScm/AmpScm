using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    internal class WaitForDataBucket : ProxyBucket<WaitForDataBucket>, IBucketAggregation, IBucketWriter
    {
        bool _waitingForMore;
        bool _readEof;
        TaskCompletionSource<bool>? _waiter;
        readonly object _l = new();

        public WaitForDataBucket()
            : base(new AggregateBucket())
        {
            Aggregation = (AggregateBucket)Inner;
        }
        AggregateBucket Aggregation { get; }

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

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
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

                lock(_l)
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
}
