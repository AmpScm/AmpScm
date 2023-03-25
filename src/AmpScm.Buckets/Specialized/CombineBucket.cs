using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    public abstract class CombineBucket : WrappingBucket
    {
        protected Bucket Left => Source;
        protected Bucket Right { get; }

        protected CombineBucket(Bucket left, Bucket right)
            : base(left)
        {
            Right = right ?? throw new ArgumentNullException(nameof(right));
        }

        protected override void InnerDispose()
        {
            try
            {
                Right.Dispose();
            }
            finally
            {
                base.InnerDispose();
            }
        }

    }
}
