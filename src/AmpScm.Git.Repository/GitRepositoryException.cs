using System.Runtime.Serialization;

namespace AmpScm.Git;

[Serializable]
public class GitRepositoryException : GitException
{
    public GitRepositoryException()
    {

    }
    public GitRepositoryException(string message) : base(message)
    {
    }

    public GitRepositoryException(string message, Exception innerexception) : base(message, innerexception)
    {
    }

    [Obsolete("Just for legacy .Net compatibilty")]
    protected GitRepositoryException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
