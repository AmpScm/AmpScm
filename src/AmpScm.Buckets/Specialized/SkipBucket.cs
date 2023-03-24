using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class SkipBucket : PositionBucket, IBucketSkip
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly bool _ensure;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _skipped;

        public long FirstPosition { get; private set; }

        public SkipBucket(Bucket inner, long firstPosition, bool ensure) : base(inner)
        {
            if (firstPosition <= 0)
                throw new ArgumentOutOfRangeException(nameof(firstPosition));

            FirstPosition = firstPosition;
            _ensure = ensure;
        }

        public Bucket Skip(long firstPosition, bool ensure)
        {
            if (firstPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(firstPosition));

            if (Position == 0)
            {
                FirstPosition += firstPosition;
                return this;
            }
            else
                return new SkipBucket(this, firstPosition, ensure);
        }

        public override long? Position => Math.Max(0L, base.Position!.Value - FirstPosition);

        public override BucketBytes Peek()
        {
            if (_skipped)
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
            if (_skipped)
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

        public override ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            if (_skipped)
                return base.ReadAsync(requested);
            else
                return SkipReadAsync(requested);
        }

        public override void Reset()
        {
            base.Reset();
            _skipped = false;
        }

        private async ValueTask<BucketBytes> SkipReadAsync(int requested)
        {
            long skip = FirstPosition - base.Position!.Value;

            if (skip > 0)
            {
                long skipped = await ReadSkipAsync(skip).ConfigureAwait(false);

                if (skip != skipped)
                {
                    if (!_ensure)
                        return BucketBytes.Eof;
                    else
                        throw new BucketEofException(Inner);
                }
            }
            _skipped = true;

            return await base.ReadAsync(requested).ConfigureAwait(false);
        }

        internal static Bucket SeekOnReset(Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            long? p = bucket.Position;

            if (p.HasValue)
            {
                var sb = new SkipBucket(bucket, p.Value, true);
                sb.CurrentPosition = p.Value;
                return sb;
            }
            else
                throw new ArgumentException("Passed bucket must support position", nameof(bucket));
        }

        protected override PositionBucket NewPositionBucket(Bucket duplicatedInner)
        {
            return new SkipBucket(duplicatedInner, FirstPosition, _ensure);
        }
    }
}
