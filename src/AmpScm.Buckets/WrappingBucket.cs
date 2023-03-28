using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets
{
    public abstract class WrappingBucket : Bucket, IBucketNoDispose
    {
        protected Bucket Source { get; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _nDispose;

        protected WrappingBucket(Bucket source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            _nDispose = 1;
        }

        protected WrappingBucket(Bucket source, bool noDispose)
            : this(source)
        {
            if (noDispose)
                NoDispose();
        }

        public override string Name => base.Name + ">" + Source.Name;

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
            Source.Dispose();
        }

        protected Bucket NoDispose()
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

        internal Bucket GetSourceBucket()
        {
            return Source;
        }
    }
}
