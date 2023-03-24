using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Security;

[Serializable]
public class BucketDecryptException : BucketDecodeException
{
    public BucketDecryptException(string message) : base(message)
    {
    }

    public BucketDecryptException(string message, Exception innerException) : base(message, innerException)
    {
    }

    protected BucketDecryptException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
