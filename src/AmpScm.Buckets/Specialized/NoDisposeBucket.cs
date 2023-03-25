namespace AmpScm.Buckets.Specialized
{
    internal sealed class NoDisposeBucket : Specialized.ProxyBucket<NoDisposeBucket>.WithPoll
    {
        public NoDisposeBucket(Bucket inner) : base(inner, true)
        {
        }

        public override string Name => "NoDispose>" + Source.Name;

        protected override NoDisposeBucket? WrapDuplicate(Bucket duplicatedInner, bool reset)
        {
            return null; // Yes the duplicate *is* owned, otherwise it wouldn't have an owner
        }
    }
}
