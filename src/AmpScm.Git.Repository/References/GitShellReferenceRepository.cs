namespace AmpScm.Git.References;

/// <summary>
/// There are some custom backends used in some places to improve over the current packaged
/// references. For now we fall back through to the shell handling as last resort to at least
/// have all references, until we have implemented the new db format
/// </summary>
internal class GitShellReferenceRepository : GitPackedRefsReferenceRepository
{
    public GitShellReferenceRepository(GitReferenceRepository repository, string gitDir, string workTreeDir)
        : base(repository, gitDir, workTreeDir)
    {
    }

    private protected override async ValueTask ReadRefs()
    {
        GitRefPeel? last = null;
        var idLength = GitId.HashLength(Repository.InternalConfig.IdType) * 2;

#pragma warning disable CA1861 // Avoid constant arrays as arguments
        await foreach (var line in Repository.WalkPlumbingCommand("show-ref", new[] {"-d", "--head"}, 
            expectedResults: new int[] { 0 /* ok */, 1 /* no references found */}).ConfigureAwait(false))
        {
            ParseLineToPeel(line.Trim(), ref last, idLength);
        }
#pragma warning restore CA1861 // Avoid constant arrays as arguments
    }
}
