using System.Runtime.Serialization;

namespace AmpScm.Buckets;

[Serializable]
public class BucketException : Exception
{
    public BucketException()
    {
    }

    public BucketException(string? message) : base(message)
    {
    }

    public BucketException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    [Obsolete("Just for legacy .Net compatibilty")]
    protected BucketException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
