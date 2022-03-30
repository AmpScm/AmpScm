﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    public abstract class CombineBucket : WrappingBucket
    {
        protected Bucket Left => Inner;
        protected Bucket Right { get; }

        protected CombineBucket(Bucket left, Bucket right)
            : base(left)
        {
            Right = right ?? throw new ArgumentNullException(nameof(right));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && !DontDisposeInner)
                Right.Dispose();
        }
    }
}
