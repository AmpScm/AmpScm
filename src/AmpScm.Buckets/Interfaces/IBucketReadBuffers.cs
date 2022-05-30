using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Interfaces
{
    public interface IBucketReadBuffers
    {
        ValueTask<(ReadOnlyMemory<byte>[] Buffers, bool Done)> ReadBuffersAsync(int requested = Bucket.MaxRead);
    }
}
