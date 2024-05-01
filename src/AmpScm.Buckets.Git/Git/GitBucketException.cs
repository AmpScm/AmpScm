using System.Runtime.Serialization;
using AmpScm.Git;

namespace AmpScm.Buckets.Git;

[Serializable]
public class GitBucketException : GitException
{
    public GitBucketException()
    {
    }

    public GitBucketException(string? message)
        : base(message)
    {


    }

    public GitBucketException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    [Obsolete("Just for legacy .Net compatibilty")]
    protected GitBucketException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
