using System.Runtime.Serialization;

namespace AmpScm.Buckets.Client;

[Serializable]
public class BucketClientException : Exception
{
    public BucketClientException()
    {
    }

    public BucketClientException(string message) : base(message)
    {
    }

    public BucketClientException(string message, Exception innerException) : base(message, innerException)
    {
    }

    [Obsolete("Just for legacy .Net compatibilty")]
    protected BucketClientException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
