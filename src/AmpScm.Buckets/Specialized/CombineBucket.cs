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
        protected Bucket LeftSource => Source;
        protected Bucket RightSource { get; }

        protected CombineBucket(Bucket leftSource, Bucket rightSource)
            : base(leftSource)
        {
            RightSource = rightSource ?? throw new ArgumentNullException(nameof(rightSource));
        }

        protected override void InnerDispose()
        {
            try
            {
                RightSource.Dispose();
            }
            finally
            {
                base.InnerDispose();
            }
        }

    }
}
