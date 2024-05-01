using System.Runtime.Serialization;

namespace AmpScm.Git;

[Serializable]
public class GitException : Exception
{
    public GitException()
    {
    }

    public GitException(string? message)
        : base(message)
    {


    }

    public GitException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    [Obsolete("Just for legacy .Net compatibilty")]
    protected GitException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
