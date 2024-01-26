using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Interfaces;

internal interface IBucketTake
{
    Bucket Take(long limit, bool ensure = true);
}
