using System.Diagnostics;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets;

[DebuggerTypeProxy(typeof(AggregateDebugProxy))]
[DebuggerDisplay($"{{{nameof(Name)},nq}}: {{{nameof(BucketCount)}}} Buckets, Current={{{nameof(CurrentBucket)},nq}}")]
public partial class AggregateBucket : Bucket, IBucketAggregation, IBucketReadBuffers, IBucketNoDispose
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ValueList<Bucket> _buckets;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _n;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly bool _keepOpen;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private long _position;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private object LockOn => this;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _nDispose;

    public AggregateBucket(params Bucket[] sources)
    {
        if (sources is null)
            throw new ArgumentNullException(nameof(sources));

        _buckets = new();
        _nDispose = 1;
        _buckets.AddRange(sources);
    }

    public AggregateBucket(bool keepOpen, params Bucket[] sources)
        : this(sources)
    {
        _keepOpen = keepOpen;
    }

    public Bucket Append(Bucket source)
    {
        if (source is null || source is EmptyBucket)
            return this;

        lock (LockOn)
        {
            _buckets.Add(source);
        }

        return this;
    }

    public Bucket Prepend(Bucket source)
    {
        if (source is null || source is EmptyBucket)
            return this;

        if (_keepOpen)
            return new SimpleAggregate(source, this);
        else
            lock (LockOn)
            {
                _buckets.Insert(0, source);
            }
        return this;
    }

    public override bool CanReset => _keepOpen && _buckets.All(x => x!.CanReset);

    public override void Reset()
    {
        if (!_keepOpen)
            throw new InvalidOperationException();

        if (_n >= _buckets.Count)
            _n = _buckets.Count - 1;

        while (_n >= 0)
        {
            _buckets[_n]!.Reset();
            _n--;
        }
        _n = 0;
        _position = 0;
    }

    private Bucket? CurrentBucket
    {
        get
        {
            lock (LockOn)
            {
                if (_n < _buckets.Count)
                    return _buckets[_n];
                else
                    return null;
            }
        }
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        if (!_keepOpen && _n > 0)
        {
            List<IAsyncDisposable>? disposables = null;
            lock (LockOn)
            {
                while (_n > 0)
                {
                    var del = _buckets[0];
                    _buckets.RemoveAt(0);
                    _n--;
                    if (del != null)
                    {
                        disposables ??= new();
                        disposables.Add(del);
                    }
                }
            }

            if (disposables is { })
            {
                foreach (var d in disposables)
                {
                    await d.DisposeAsync().ConfigureAwait(false);
                }
            }
        }


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
                _buckets.RemoveAt(_n--);
            else
                del = null;

            _n++;
        }

        if (del != null)
            del.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public override ValueTask<long> ReadSkipAsync(long requested)
    {
        return base.ReadSkipAsync(requested);
    }

    public override async ValueTask<long?> ReadRemainingBytesAsync()
    {
        int n = _n;
        long remaining = 0;

        while (n < _buckets.Count)
        {
            long? r = await _buckets[n]!.ReadRemainingBytesAsync().ConfigureAwait(false);

            if (!r.HasValue)
                return null;

            remaining += r.Value;
            n++;
        }
        return remaining;
    }

    public override BucketBytes Peek()
    {
        while (true)
        {
            if (CurrentBucket is Bucket cur)
            {
                var v = cur.Peek();

                if (!v.IsEof)
                    return v;
            }
            else
                return BucketBytes.Eof;

            MoveNext(close: true);
        }
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        try
        {
            if (disposing)
            {
                int n = Interlocked.Decrement(ref _nDispose);

                if (n == 0)
                {
                    try
                    {
                        await InnerDisposeAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException oe)
                    {
                        throw new ObjectDisposedException($"While disposing {SafeName}", oe);

                    }
                }
#if DEBUG && NET7_0_OR_GREATER
                else
                    ObjectDisposedException.ThrowIf(n < 0, this);
#elif DEBUG
                else if (n < 0)
                    throw new ObjectDisposedException(SafeName);
#endif
            }
        }
        finally
        {
            await base.DisposeAsync(disposing).ConfigureAwait(false);
        }
    }

    protected virtual async ValueTask InnerDisposeAsync()
    {
        for (int i = _buckets.Count - 1; i >= 0; i--)
        {
            if (_buckets[i] is Bucket cur)
                await cur.DisposeAsync().ConfigureAwait(false);
            _buckets.RemoveAt(i);
        }
        _n = 0;
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

        var ab = new AggregateBucket(keepOpen: true, newBuckets.ToArray());
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
        {
            while (_n > 0)
            {
                var del = _buckets[0];
                _buckets.RemoveAt(0);
                _n--;
                if (del is { })
                    await del.DisposeAsync().ConfigureAwait(false);
            }
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
                MoveNext(close: false);
            }
        }

        if (!result.Any())
        {
            while (!result.Any() && CurrentBucket is not null)
            {
                var bb = await CurrentBucket.ReadAsync(maxRequested).ConfigureAwait(false);

                if (bb.IsEof)
                    MoveNext();
                else
                {
                    return (new[] { bb.Memory }, false);
                }
            }
        }

        return (result.ToArray(), CurrentBucket is null);
    }

    Bucket IBucketNoDispose.NoDispose()
    {
        NoDispose();
        return this;
    }

    protected void NoDispose()
    {
        Interlocked.Increment(ref _nDispose);
    }

    bool IBucketNoDispose.HasMultipleDisposers()
    {
        return HasMultipleDisposers();
    }

    protected bool HasMultipleDisposers()
    {
        return _nDispose > 1 || _keepOpen;
    }
}
