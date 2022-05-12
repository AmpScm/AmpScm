using System;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    public abstract class WrappingBucket : Bucket
    {
        protected Bucket Inner { get; }
        protected internal bool DontDisposeInner { get; internal set; }

        protected WrappingBucket(Bucket inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        protected WrappingBucket(Bucket inner, bool noDispose)
            : this(inner)
        {
            DontDisposeInner = noDispose;
        }

        public override string Name => base.Name + ">" + Inner.Name;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && !DontDisposeInner)
            {
                DontDisposeInner = true;
                Inner.Dispose();
            }
        }

        protected void NoClose()
        {
            DontDisposeInner = true;
        }
    }
}
