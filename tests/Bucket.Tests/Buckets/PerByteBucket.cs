using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;

namespace AmpScm.BucketTests.Buckets
{
    public sealed class PerByteBucket : ProxyBucket<PerByteBucket>
    {
        public PerByteBucket(Bucket inner) : base(inner)
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

        public override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            return base.ReadAsync(1);
        }

        public override ValueTask<int> ReadSkipAsync(int requested)
        {
            return base.ReadSkipAsync(1);
        }
    }
}
