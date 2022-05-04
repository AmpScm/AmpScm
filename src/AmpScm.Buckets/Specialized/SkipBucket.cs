using System;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class SkipBucket : PositionBucket, IBucketSkip
    {
        public long FirstPosition { get; private set; }

        public SkipBucket(Bucket inner, long firstPosition) : base(inner)
        {
            if (firstPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(firstPosition));

            FirstPosition = firstPosition;
        }

        public Bucket Skip(long firstPosition)
        {
            if (firstPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(firstPosition));

            if (Position == 0)
            {
                FirstPosition += firstPosition;
                return this;
            }
            else
                return new SkipBucket(this, firstPosition);
        }

        public override long? Position => Math.Max(0L, base.Position!.Value - FirstPosition);

        public override BucketBytes Peek()
        {
            if (base.Position >= FirstPosition)
                return Inner.Peek();

            var b = Inner.Peek();

            if (b.Length > 0)
            {
                long skip = FirstPosition - base.Position!.Value;

                if (skip < b.Length)
                    return b.Slice((int)skip);
                else
                    return BucketBytes.Empty;
            }
            else
                return BucketBytes.Empty;
        }

        public override async ValueTask<BucketBytes> PollAsync(int minRequested = 1)
        {
            if (base.Position >= FirstPosition)
                return await Inner.PollAsync(minRequested).ConfigureAwait(false);

            var b = await Inner.PollAsync(minRequested).ConfigureAwait(false);

            if (b.Length > 0)
            {
                long skip = FirstPosition - base.Position!.Value;

                if (skip < b.Length)
                    return b.Slice((int)skip);
                else
                    return BucketBytes.Empty;
            }
            else
                return BucketBytes.Empty;
        }

        public override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (base.Position >= FirstPosition)
                return base.ReadAsync(requested);
            else
                return SkipReadAsync(requested);
        }

        public override ValueTask ResetAsync()
        {
            return base.ResetAsync();
        }

        private async ValueTask<BucketBytes> SkipReadAsync(int requested)
        {
            long skip = FirstPosition - base.Position!.Value;

            skip -= await ReadSkipAsync(skip).ConfigureAwait(false);

            if (skip > 0)
                return BucketBytes.Eof;

            return await base.ReadAsync(requested).ConfigureAwait(false);
        }

        internal static Bucket SeekOnReset(Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            var p = bucket.Position;

            if (p.HasValue)
            {
                var sb = new SkipBucket(bucket, p.Value);
                sb.SetPosition(p.Value);
                return sb;
            }
            else
                throw new ArgumentException("Passed bucket must support position", nameof(bucket));
        }

        protected override PositionBucket NewPositionBucket(Bucket duplicatedInner)
        {
            return new SkipBucket(duplicatedInner, FirstPosition);
        }
    }
}
