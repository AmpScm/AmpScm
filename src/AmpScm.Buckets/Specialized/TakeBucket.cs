﻿using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized;

internal sealed class TakeBucket : PositionBucket, IBucketTake
{
    public long Limit { get; private set; }

    private bool _ensure;

    public TakeBucket(Bucket source, long limit, bool ensure)
        : base(source)
    {
        if (limit < 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, message: null);

        Limit = limit;
        _ensure = ensure;
    }

    public Bucket Take(long limit, bool ensure = true)
    {
        if (limit < 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, message: null);

        if (ensure)
        {
            if (limit > Limit)
                throw new BucketException($"Can't read {limit} bytes from a bucket that is limited to {Limit}");

            Limit = limit;
        }
        else
        {
            if (limit < Limit)
                Limit = limit;
        }
        _ensure = ensure;

        return this;
    }

    public override BucketBytes Peek()
    {
        var peek = Source.Peek();

        if (peek.Length <= 0)
            return peek;

        long pos = Position!.Value;

        if (Limit - pos < peek.Length)
            return peek.Slice(0, (int)(Limit - pos));

        return peek;
    }

    public override async ValueTask<BucketBytes> PollAsync(int minRequested = 1)
    {
        var poll = await Source.PollAsync().ConfigureAwait(false);

        if (poll.Length <= 0)
            return poll;

        long pos = Position!.Value;

        if (Limit - pos < poll.Length)
            return poll.Slice(0, (int)(Limit - pos));

        return poll;
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        long pos = Position!.Value;

        if (pos >= Limit)
            return BucketBytes.Eof;

        if (Limit - pos < requested)
            requested = (int)(Limit - pos);

        var bb = await base.ReadAsync(requested).ConfigureAwait(false); // Position updated in base

        if (bb.IsEof && _ensure)
            throw new BucketEofException(Source);

        return bb;
    }

    public override ValueTask<long> ReadSkipAsync(long requested)
    {
        long pos = Position!.Value;

        if (pos >= Limit) return new(0);

        if (Limit - pos < requested)
            requested = Limit - pos;

        return base.ReadSkipAsync(requested);
    }

    public override async ValueTask<long?> ReadRemainingBytesAsync()
    {
        long pos = Position!.Value;

        if (pos >= Limit)
            return 0L;

        long limit = Limit - pos;

        if (_ensure)
            return limit;

        long? l = await base.ReadRemainingBytesAsync().ConfigureAwait(false);

        if (!l.HasValue)
            return null;

        return Math.Min(limit, l.Value);
    }

    protected override PositionBucket NewPositionBucket(Bucket duplicatedInner)
    {
        return new TakeBucket(duplicatedInner, Limit, _ensure);
    }
}
