﻿using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    public abstract class ProxyBucket<TBucket> : WrappingBucket, IBucketNoClose
        where TBucket : Bucket
    {
        protected ProxyBucket(Bucket inner)
            : base(inner)
        {
        }

        internal ProxyBucket(Bucket inner, bool noDispose)
            : base(inner, noDispose)
        {
        }

        public override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            return Inner.ReadAsync(requested);
        }

        public override bool CanReset => Inner.CanReset;

        public override BucketBytes Peek()
        {
            return Inner.Peek();
        }

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            return Inner.ReadRemainingBytesAsync();
        }

        public override long? Position => Inner.Position;

        public override ValueTask<int> ReadSkipAsync(int requested)
        {
            return Inner.ReadSkipAsync(requested);
        }

        public override ValueTask ResetAsync()
        {
            return Inner.ResetAsync();
        }

        public override async ValueTask<Bucket> DuplicateAsync(bool reset)
        {
            var r = await Inner.DuplicateAsync(reset).ConfigureAwait(false);
            return WrapDuplicate(r, reset) ?? r;
        }

        public override ValueTask<(BucketBytes, BucketEol)> ReadUntilEolAsync(BucketEol acceptableEols, int requested = int.MaxValue)
        {
            return Inner.ReadUntilEolAsync(acceptableEols, requested);
        }

        public override TReadBucket? ReadBucket<TReadBucket>()
            where TReadBucket : class
        {
            return Inner.ReadBucket<TReadBucket>();
        }

        protected virtual TBucket? WrapDuplicate(Bucket duplicatedInner, bool reset)
        {
            return null;
        }

        public new virtual Bucket NoClose()
        {
            base.NoClose();
            return this;
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
                return Inner.PollAsync(minRequested);
            }
        }
    }

    public class ProxyBucket : ProxyBucket<ProxyBucket>
    {
        string? _name;
        public ProxyBucket(Bucket inner) : base(inner)
        {

        }

        public override string Name => _name ?? (_name = (GetType() == typeof(ProxyBucket) ? "Proxy" : base.Name) + ">" + Inner.Name);


        internal ProxyBucket(Bucket inner, bool noDispose) : base(inner, noDispose)
        {
        }


        internal sealed class Sealed : ProxyBucket, IBucketPoll
        {
            public Sealed(Bucket inner) : base(inner)
            {
            }

            public override string Name => "Proxy>" + Inner.Name;

            public ValueTask<BucketBytes> PollAsync(int minRequested = 1)
            {
                return Inner.PollAsync(minRequested);
            }
        }
    }
}
