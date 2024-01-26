using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Subversion;

public abstract class SvnBucket : WrappingBucket
{
    protected SvnBucket(Bucket source) : base(source)
    {
    }
}
