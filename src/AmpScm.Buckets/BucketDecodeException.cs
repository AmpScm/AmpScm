using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets;

[Serializable]
public class BucketDecodeException : BucketException
{
    public BucketDecodeException()
    {
    }

    public BucketDecodeException(string message) : base(message)
    {
    }

    public BucketDecodeException(string message, Exception innerException) : base(message, innerException)
    {
    }

    [Obsolete("Just for legacy .Net compatibilty")]
    protected BucketDecodeException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
