using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets
{
    public abstract class WrappingBucket : Bucket, IBucketNoDispose
    {
        /// <summary>
        /// The wrapped source <see cref="Bucket"/>
        /// </summary>
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

        protected override bool AcceptDisposing()
        {
            int n = Interlocked.Decrement(ref _nDispose);

            if (n == 0)
                return true;
#if DEBUG && NET7_0_OR_GREATER
            else
                ObjectDisposedException.ThrowIf(n < 0, this);
#elif DEBUG
            else if (n < 0)
                throw new ObjectDisposedException(SafeName);
#endif
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                    Source.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
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

        public override void Reset()
        {
            base.Reset();

            Source.Reset();
        }
    }
}
