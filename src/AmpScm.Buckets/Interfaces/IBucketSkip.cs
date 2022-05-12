using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Interfaces
{
    interface IBucketSkip
    {
        Bucket Skip(long skipBytes, bool ensure);
    }

}
