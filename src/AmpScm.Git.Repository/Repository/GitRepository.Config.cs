using System.Diagnostics;

namespace AmpScm.Git;

public partial class GitRepository
{
    internal class GitInternalConfigAccess
    {
        public GitIdType IdType { get; } = GitIdType.Sha1;

        internal GitInternalConfigAccess(GitIdType type)
        {
            IdType = type;
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal GitInternalConfigAccess InternalConfig { get; private set;  } = new GitInternalConfigAccess(GitIdType.Sha1);

    internal void SetSHA256() // Called from repository object store on config verify
    {
        InternalConfig = new GitInternalConfigAccess(GitIdType.Sha256);
    }
}
