namespace AmpScm.Git.Sets;

internal record GitRevisionSetOptions
{
    internal GitRevisionSetOptions AddCommit(GitCommit gitCommit)
    {
        if (Commits.Contains(gitCommit))
            return this;

        var c = new GitRevisionSetOptions(this);
        c.Commits.Add(gitCommit);

        return c;
    }

    public List<GitCommit> Commits { get; } = new List<GitCommit>();
}
