using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets
{
    [DebuggerDisplay("{Name,nq}: BucketCount={BucketCount}, Current={CurrentBucket,nq}, Position={Position}")]
    public class AggregateBucket : Bucket, IBucketAggregation, IBucketReadBuffers
    {
        Bucket?[] _buckets;
        int _n;
        readonly bool _keepOpen;
        long _position;

        object LockOn => this;

        public AggregateBucket(params Bucket[] items)
        {
            _buckets = items ?? Array.Empty<Bucket>();
        }

        public AggregateBucket(bool keepOpen, params Bucket[] items)
            : this(items)
        {
            _keepOpen = keepOpen;
        }

        public Bucket Append(Bucket bucket)
        {
            if (bucket is null || bucket is EmptyBucket)
                return this;

            lock (LockOn)
            {
                if (_n >= _buckets.Length && !_keepOpen)
                {
                    _buckets = new[] { bucket };
                    _n = 0;
                }
                else if (_keepOpen || _n == 0)
                    _buckets = _buckets.ArrayAppend(bucket);
                else
                {
                    int nShrink = _n;

                    var newBuckets = new Bucket[_buckets.Length - nShrink + 1];
                    if (_buckets.Length > nShrink)
                        Array.Copy(_buckets, _n, newBuckets, 0, _buckets.Length - nShrink);
                    _buckets = newBuckets;
                    newBuckets[newBuckets.Length - 1] = bucket;
                    _n -= nShrink;
                }
            }

            return this;
        }

        public Bucket Prepend(Bucket bucket)
        {
            if (bucket is null || bucket is EmptyBucket)
                return this;

            if (!_keepOpen && _n > 0)
                _buckets[--_n] = bucket;
            else if (_n > 0)
                throw new InvalidOperationException();
            {
                var newBuckets = new Bucket[_buckets.Length + 1];
                Array.Copy(_buckets, _n, newBuckets, 1, _buckets.Length);
                newBuckets[0] = bucket;
                _buckets = newBuckets;
            }
            return this;
        }

        public override bool CanReset => _keepOpen && _buckets.All(x => x!.CanReset);

        public override void Reset()
        {
            if (!_keepOpen)
                throw new InvalidOperationException();

            if (_n >= _buckets.Length)
                _n = _buckets.Length - 1;

            while (_n >= 0)
            {
                _buckets[_n]!.Reset();
                _n--;
            }
            _n = 0;
            _position = 0;
        }

        Bucket? CurrentBucket
        {
            get
            {
                lock (LockOn)
                {
                    if (_n < _buckets.Length)
                        return _buckets[_n];
                    else
                        return null;
                }
            }
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            while (CurrentBucket is Bucket cur)
            {
                var r = await cur.ReadAsync(requested).ConfigureAwait(false);

                if (!r.IsEof)
                {
                    if (r.Length == 0)
                        throw new InvalidOperationException($"Got 0 byte read on {_buckets[_n]?.Name} bucket");

                    _position += r.Length;

                    return r;
                }

                MoveNext();
            }
            if (!_keepOpen)
            {
                _buckets = Array.Empty<Bucket>();
                _n = 0;
            }
            return BucketBytes.Eof;
        }

        private void MoveNext(bool close = true)
        {
            Bucket? del;
            lock (LockOn)
            {
                del = CurrentBucket;

                if (del == null)
                    return;

                if (!_keepOpen && close)
                    _buckets[_n] = null;
                else
                    del = null;

                _n++;
            }

            del?.Dispose();
        }

        public override ValueTask<long> ReadSkipAsync(long requested)
        {
            return base.ReadSkipAsync(requested);
        }

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            int n = _n;
            long remaining = 0;

            while (n < _buckets.Length)
            {
                var r = await _buckets[n]!.ReadRemainingBytesAsync().ConfigureAwait(false);

                if (!r.HasValue)
                    return null;

                remaining += r.Value;
                n++;
            }
            return remaining;
        }

        public override BucketBytes Peek()
        {
            if (CurrentBucket is Bucket cur)
            {
                var v = cur.Peek();

                if (!v.IsEof)
                    return v;

            }

            lock (LockOn)
            {
                int n = _n + 1;
                while (n < _buckets.Length)
                {
                    var v = _buckets[n]!.Peek();

                    if (!v.IsEof)
                        return v;

                    n++; // Peek next bucket, but do not dispose. We can do that in the next read
                }
            }

            return BucketBytes.Eof;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                for (int i = 0; i < _buckets.Length; i++)
                {
                    _buckets[i]?.Dispose();
                    _buckets[i] = null;
                }

                _buckets = Array.Empty<Bucket>();
                _n = 0;
            }
            base.Dispose(disposing);
        }

        public override long? Position
        {
            get => _position;
        }

        public override Bucket Duplicate(bool reset = false)
        {
            if (!_keepOpen)
                throw new NotSupportedException();
            else if (reset && !CanReset)
                throw new InvalidOperationException();

            var newBuckets = new List<Bucket>();

            foreach (var v in _buckets)
                newBuckets.Add(v!.Duplicate(reset));

            var ab = new AggregateBucket(true, newBuckets.ToArray());
            if (!reset)
                ab._position = _position;
            return ab;
        }

#pragma warning disable CA1033 // Interface methods should be callable by child types
        async ValueTask<(ReadOnlyMemory<byte>[] Buffers, bool Done)> IBucketReadBuffers.ReadBuffersAsync(int maxRequested)
#pragma warning restore CA1033 // Interface methods should be callable by child types
        {
            IEnumerable<ReadOnlyMemory<byte>>? result = Enumerable.Empty<ReadOnlyMemory<byte>>();

            if (!_keepOpen)

                for (int i = _n - 1; i >= 0 && _buckets[i] != null; i--)
                {
                    var del = _buckets[i]!;
                    _buckets[i] = null;

                    del.Dispose();
                }

            while (CurrentBucket is IBucketReadBuffers iov)
            {
                var r = await iov.ReadBuffersAsync(maxRequested).ConfigureAwait(false);

                if (r.Buffers.Length > 0)
                    result = (result != null) ? result.Concat(r.Buffers) : r.Buffers;

                maxRequested -= r.Buffers.Sum(x => x.Length);

                if (!r.Done || maxRequested == 0)
                {
                    return (result.ToArray(), false); // Don't want to wait. Done for now
                }
                else
                {
                    MoveNext(false);
                }
            }

            return (result.ToArray(), CurrentBucket is null);
        }

        #region DEBUG INFO
        int BucketCount => _buckets.Length - _n;
        #endregion
    }
}
