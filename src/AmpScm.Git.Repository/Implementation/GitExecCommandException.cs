using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;

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
