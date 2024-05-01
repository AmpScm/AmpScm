using System;
using System.Linq;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized;

internal sealed class StrictCompareBucket : BlockCombineBucket
{
    public StrictCompareBucket(Bucket leftSource, Bucket rightSource) : base(leftSource, rightSource)
    {
    }

    protected override ValueTask<BucketBytes> ProcessAsync(BucketBytes left, BucketBytes right)
    {
        if (!left.Span.SequenceEqual(right.Span))
        {
            throw new BucketException($"left doesn't match right");
        }

        return left;
    }

    public override BucketBytes Peek()
    {
        return Source.Peek();
    }
}
