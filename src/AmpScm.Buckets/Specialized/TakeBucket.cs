using System;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class TakeBucket : PositionBucket, IBucketTake
    {
        public long Limit { get; private set; }

        public TakeBucket(Bucket inner, long limit)
            : base(inner)
        {
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            Limit = limit;
        }

        public Bucket Take(long limit)
        {
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            if (limit < Limit)
                Limit = limit;

            return this;
        }

        public override BucketBytes Peek()
        {
            var peek = Inner.Peek();

            if (peek.Length <= 0)
                return peek;

            long pos = Position!.Value;

            if (Limit - pos < peek.Length)
                return peek.Slice(0, (int)(Limit - pos));

            return peek;
        }

        public override async ValueTask<BucketBytes> PollAsync(int minRequested = 1)
        {
            var poll = await Inner.PollAsync().ConfigureAwait(false);

            if (poll.Length <= 0)
                return poll;

            long pos = Position!.Value;

            if (Limit - pos < poll.Length)
                return poll.Slice(0, (int)(Limit - pos));

            return poll;
        }

        public override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            long pos = Position!.Value;

            if (pos >= Limit)
                return BucketBytes.Eof;

            if (Limit - pos < requested)
                requested = (int)(Limit - pos);

            return base.ReadAsync(requested); // Position updated in base
        }

        public override ValueTask<long> ReadSkipAsync(long requested)
        {
            long pos = Position!.Value;

            if (pos >= Limit) return new ValueTask<long>(0);

            if (Limit - pos < requested)
                requested = (long)(Limit - pos);

            return base.ReadSkipAsync(requested);
        }

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            long pos = Position!.Value;

            if (pos >= Limit)
                return 0L;

            var limit = Limit - pos;
            var l = await base.ReadRemainingBytesAsync().ConfigureAwait(false);

            if (!l.HasValue)
                return null;

            return Math.Min(limit, l.Value);
        }

        protected override PositionBucket NewPositionBucket(Bucket duplicatedInner)
        {
            return new TakeBucket(duplicatedInner, Limit);
        }
    }
}
