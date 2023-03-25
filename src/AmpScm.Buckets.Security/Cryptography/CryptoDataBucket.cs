using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Cryptography
{
    public abstract class CryptoDataBucket : WrappingBucket
    {
        private protected CryptoDataBucket(Bucket source) : base(source)
        {
        }
    }
}
