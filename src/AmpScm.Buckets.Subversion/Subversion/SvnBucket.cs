using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Subversion
{
    public abstract class SvnBucket : WrappingBucket
    {
        protected SvnBucket(Bucket inner) : base(inner)
        {
        }
    }
}
