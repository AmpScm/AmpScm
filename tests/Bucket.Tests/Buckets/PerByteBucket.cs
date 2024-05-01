using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;

namespace AmpScm.BucketTests.Buckets;

public sealed class PerByteBucket : ProxyBucket<PerByteBucket>
{
    public PerByteBucket(Bucket source) : base(source)
    {
    }

    public override BucketBytes Peek()
    {
        var b = base.Peek();

        if (b.Length > 1)
            return b.Slice(0, 1);
        else
            return b;
    }

    public override ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        return base.ReadAsync(1);
    }
}
