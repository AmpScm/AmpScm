using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;

namespace BucketTests.Buckets;

internal sealed class NoPositionBucket : ProxyBucket
{
    public NoPositionBucket(Bucket inner) : base(inner)
    {
    }

    public override long? Position => null;
}

internal sealed class NoRemainingBucket : ProxyBucket
{
    public NoRemainingBucket(Bucket inner) : base(inner)
    {
    }

    public override ValueTask<long?> ReadRemainingBytesAsync()
    {
        return new((long?)null);
    }
}
