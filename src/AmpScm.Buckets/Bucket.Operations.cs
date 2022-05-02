using System;
using System.Threading.Tasks;

namespace AmpScm.Buckets
{
    public partial class Bucket
    {
        internal async ValueTask<byte?> ReadByteAsync()
        {
            var bb = await ReadAsync(1).ConfigureAwait(false);

            if (bb.Length != 1)
                return null;
            else
                return bb[0];
        }
    }
}
