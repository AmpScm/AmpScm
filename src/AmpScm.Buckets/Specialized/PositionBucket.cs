using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized;

internal class PositionBucket : ProxyBucket<PositionBucket>.WithPoll
{
    private protected long CurrentPosition { get; set; }

    public PositionBucket(Bucket source)
        : base(source)
    {
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        var v = await Source.ReadAsync(requested).ConfigureAwait(false);

        CurrentPosition += v.Length;
        return v;
    }

    public override async ValueTask<long> ReadSkipAsync(long requested)
    {
        long v = await Source.ReadSkipAsync(requested).ConfigureAwait(false);

        CurrentPosition += v;
        return v;
    }

    public override void Reset()
    {
        base.Reset();
        CurrentPosition = 0;
    }

    protected override sealed PositionBucket? WrapDuplicate(Bucket duplicatedInner, bool reset)
    {
        var p = NewPositionBucket(duplicatedInner);
        if (!reset)
            p.CurrentPosition = CurrentPosition;

        return p;
    }

    protected virtual PositionBucket NewPositionBucket(Bucket duplicatedInner)
    {
        return new PositionBucket(duplicatedInner);
    }

    protected void SetPosition(long position)
    {
        CurrentPosition = position;
    }

    public override long? Position => CurrentPosition;
}
