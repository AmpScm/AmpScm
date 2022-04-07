using System;
using System.Threading.Tasks;

namespace AmpScm.Buckets
{
    public partial class Bucket
    {
        internal async ValueTask<byte?> ReadByteAsync()
        {
            var r = await ReadAsync(1).ConfigureAwait(false);

            if (r.Length != 1)
                return null;
            else
                return r[0];
        }
    }
}
