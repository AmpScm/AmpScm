using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Interfaces
{
    public interface IBucketIovec
    {
        ValueTask<(ReadOnlyMemory<byte>[] Buffers, bool Done)> ReadIovec(int maxRequested = int.MaxValue);
    }
}
