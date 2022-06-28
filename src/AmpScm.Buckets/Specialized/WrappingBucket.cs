using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    public abstract class WrappingBucket : Bucket, IBucketNoDispose
    {
        protected Bucket Inner { get; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        int _nDispose;

        protected WrappingBucket(Bucket inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _nDispose = 1;
        }

        protected WrappingBucket(Bucket inner, bool noDispose)
            : this(inner)
        {
            if (noDispose)
                NoDispose();
        }

        public override string Name => base.Name + ">" + Inner.Name;

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    int n = Interlocked.Decrement(ref _nDispose);

                    if (n == 0)
                        try
                        {
                            InnerDispose();
                        }
                        catch (ObjectDisposedException oe)
                        {
                            throw new ObjectDisposedException($"While disposing {SafeName}", oe);

                        }
#if DEBUG
                    else if (n < 0)
                        throw new ObjectDisposedException(SafeName);
#endif
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        protected virtual void InnerDispose()
        {
            Inner.Dispose();
        }

        protected virtual Bucket NoDispose()
        {
            Interlocked.Increment(ref _nDispose);
            return this;
        }

        protected virtual bool HasMultipleDisposers()
        {
            return _nDispose > 1;
        }

        bool IBucketNoDispose.HasMultipleDisposers() => HasMultipleDisposers();

        Bucket IBucketNoDispose.NoDispose() => NoDispose();

        internal Bucket GetInner()
        {
            return Inner;
        }
    }
}
