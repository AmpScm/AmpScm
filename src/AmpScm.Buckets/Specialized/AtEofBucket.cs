﻿namespace AmpScm.Buckets.Specialized;

internal sealed class AtEofBucket : ProxyBucket.WithPoll
{
    private Func<Task>? _atEof;
    public AtEofBucket(Bucket source, Func<Task> atEof) : base(source)
    {
        _atEof = atEof;
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        var bb = await base.ReadAsync(requested).ConfigureAwait(false);

        if (bb.IsEof)
        {
            _atEof?.Invoke();
            _atEof = null;
        }
        return bb;
    }

    public override async ValueTask<long> ReadSkipAsync(long requested)
    {
        long r = await base.ReadSkipAsync(requested).ConfigureAwait(false);

        if (r < requested)
        {
            _atEof?.Invoke();
            _atEof = null;
        }

        return r;
    }

    public override bool CanReset => false;
}
