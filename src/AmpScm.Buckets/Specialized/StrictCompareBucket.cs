using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class StrictCompareBucket : BlockCombineBucket
    {
        public StrictCompareBucket(Bucket left, Bucket right) : base(left, right)
        {
        }

        protected override ValueTask<BucketBytes> ProcessAsync(BucketBytes left, BucketBytes right)
        {
            if (!left.Span.SequenceEqual(right.Span))
            {
                throw new BucketException($"{left} doesn't match {right}");
            }

            return left;
        }

        public override BucketBytes Peek()
        {
            return Inner.Peek();
        }
    }
}
