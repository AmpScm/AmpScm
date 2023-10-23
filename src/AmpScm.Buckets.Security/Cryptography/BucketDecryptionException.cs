using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Cryptography;

[Serializable]
public class BucketDecryptionException : BucketDecodeException
{
    public BucketDecryptionException()
    {
    }

    public BucketDecryptionException(string message) : base(message)
    {
    }

    public BucketDecryptionException(string message, Exception innerException) : base(message, innerException)
    {
    }

    [Obsolete("Just for legacy .Net compatibilty")]
    protected BucketDecryptionException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
