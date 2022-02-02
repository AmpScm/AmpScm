﻿namespace AmpScm.Buckets.Specialized
{
    public sealed class NoCloseBucket : Specialized.ProxyBucket<NoCloseBucket>
    {
        public NoCloseBucket(Bucket inner) : base(inner, true)
        {
        }

        public override string Name => "NoClose>" + Inner.Name;

        protected override NoCloseBucket? WrapDuplicate(Bucket duplicatedInner, bool reset)
        {
            return null; // Yes the duplicate *is* owned, otherwise it wouldn't have an owner
        }
    }
}