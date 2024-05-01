using System.Runtime.Serialization;

namespace AmpScm.Buckets;

[Serializable]
public class HttpBucketException : Exception
{
    public HttpBucketException()
    {
    }

    public HttpBucketException(string message)
        : base(message)
    {


    }

    public HttpBucketException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    [Obsolete("Just for legacy .Net compatibilty")]
    protected HttpBucketException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
