using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets;

public partial class AggregateBucket
{
    internal sealed class SimpleAggregate : AggregateBucket, IBucketPoll
    {
        public SimpleAggregate(params Bucket[] items)
            : base(items)
        {
            if (items.Any(x => x is SimpleAggregate))
                throw new InvalidOperationException();
        }

        public SimpleAggregate(bool keepOpen, params Bucket[] items)
            : base(keepOpen, items)
        {
            if (!keepOpen && items.Any(x => x is SimpleAggregate))
                throw new InvalidOperationException();
        }

        public async ValueTask<BucketBytes> PollAsync(int minRequested = 1)
        {
            while (true)
            {
                if (CurrentBucket is Bucket cur)
                {
                    var v = await cur.PollAsync(minRequested).ConfigureAwait(false);

                    if (!v.IsEof)
                        return v;
                }
                else
                    return BucketBytes.Eof;

                MoveNext();
            }
        }

        internal Bucket[] GetBuckets()
        {
            if (_n > 0 || _buckets.Any(x => x is null))
                return _buckets.Skip(_n).Where(x => x is not null).ToArray()!;
            else
                return _buckets.ToArray()!;
        }

        public new bool HasMultipleDisposers()
        {
            return base.HasMultipleDisposers();
        }
    }

    internal void AppendRange(Bucket[] buckets, int start)
    {
        if (buckets is null)
            throw new ArgumentNullException(nameof(buckets));
        else if (start >= buckets.Length)
            return;

        lock (LockOn)
        {
            _buckets.AddRange(buckets, start);
        }
    }

    #region DEBUG INFO
    private int BucketCount => _buckets.Count - _n;

    private sealed class AggregateDebugProxy
    {
        private readonly AggregateBucket _bucket;
        public AggregateDebugProxy(AggregateBucket bucket)
        {
            _bucket = bucket;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public Bucket?[] Buckets => _bucket?._buckets.Skip(_bucket._n).ToArray() ?? Array.Empty<Bucket>();
    }
    #endregion

}
