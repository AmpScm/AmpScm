using System.Diagnostics;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    public abstract class ProxyBucket<TBucket> : WrappingBucket
        where TBucket : Bucket
    {
        protected ProxyBucket(Bucket source)
            : base(source)
        {
        }

        internal ProxyBucket(Bucket source, bool noDispose)
            : base(source, noDispose)
        {
        }

        public override ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            return Source.ReadAsync(requested);
        }

        public override bool CanReset => Source.CanReset;

        public override BucketBytes Peek()
        {
            return Source.Peek();
        }

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            return Source.ReadRemainingBytesAsync();
        }

        public override long? Position => Source.Position;

        public override ValueTask<long> ReadSkipAsync(long requested)
        {
            return Source.ReadSkipAsync(requested);
        }

        public override void Reset()
        {
            Source.Reset();
        }

        public override Bucket Duplicate(bool reset = false)
        {
            var r = Source.Duplicate(reset);
            return WrapDuplicate(r, reset) ?? r;
        }

        public override ValueTask<BucketLine> ReadUntilEolAsync(BucketEol acceptableEols, int requested = MaxRead)
        {
            return Source.ReadUntilEolAsync(acceptableEols, requested);
        }

        public override TReadBucket? ReadBucket<TReadBucket>()
            where TReadBucket : class
        {
            return Source.ReadBucket<TReadBucket>();
        }

        protected virtual TBucket? WrapDuplicate(Bucket duplicatedInner, bool reset)
        {
            return null;
        }

        internal abstract class WithPoll : ProxyBucket<TBucket>, IBucketPoll
        {
            protected WithPoll(Bucket inner) : base(inner)
            {
            }

            protected WithPoll(Bucket inner, bool noDispose) : base(inner, noDispose)
            {
            }

            public virtual ValueTask<BucketBytes> PollAsync(int minRequested = 1)
            {
                return Source.PollAsync(minRequested);
            }
        }
    }

    public class ProxyBucket : ProxyBucket<ProxyBucket>
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string? _name;

        public ProxyBucket(Bucket inner) : base(inner)
        {

        }

        public override string Name => _name ?? (_name = (GetType() == typeof(ProxyBucket) ? "Proxy" : base.Name) + ">" + Source.Name);


        internal ProxyBucket(Bucket inner, bool noDispose) : base(inner, noDispose)
        {
        }


        internal sealed class Sealed : ProxyBucket, IBucketPoll, IBucketNoDispose
        {
            public Sealed(Bucket inner) : base(inner)
            {
            }

            public override string Name => "Proxy>" + Source.Name;

            public ValueTask<BucketBytes> PollAsync(int minRequested = 1)
            {
                return Source.PollAsync(minRequested);
            }
        }
    }
}
