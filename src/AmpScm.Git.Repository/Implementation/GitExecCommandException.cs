using System.Runtime.Serialization;

namespace AmpScm.Git.Implementation;

[Serializable]
public class GitExecCommandException : GitException
{
    public GitExecCommandException()
    {
    }

    public GitExecCommandException(string message) : base(message)
    {
    }

    public GitExecCommandException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    [Obsolete("Just for legacy .Net compatibilty")]
    protected GitExecCommandException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
