using System.Diagnostics;
using System.Linq.Expressions;

namespace AmpScm.Git.Sets;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class GitRemotesSet : GitNamedSet<GitRemote>
{
    internal GitRemotesSet(GitRepository repository, Expression<Func<GitNamedSet<GitRemote>>> rootExpression)
        : base(repository, rootExpression)
    {
    }


    private string DebuggerDisplay => $"{Enumerable.Count(this)} Remotes";
}
