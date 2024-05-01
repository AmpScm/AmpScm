using System.Linq.Expressions;

namespace AmpScm.Git.Sets;

internal class GitTagSet : GitNamedSet<GitTag>
{
    internal GitTagSet(GitRepository repository, Expression<Func<GitNamedSet<GitTag>>> rootExpression) : base(repository, rootExpression)
    {
    }
}
