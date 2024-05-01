using AmpScm.Git.Sets;

namespace AmpScm.Git;

public class GitBranch : GitNamedObjectWrapper<GitCommit, GitReference>
{
    public GitReference Reference { get; }

    internal GitBranch(GitReference reference)
        : base(reference, null!)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
    }

    protected override GitCommit GitObject => Reference.Commit!;
}
