using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AmpScm.Git.References;

internal class GitRepositoryReferenceRepository : GitReferenceRepository
{
    public GitRepositoryReferenceRepository(GitRepository gitRepository, string gitDir, string workTreeDir)
        : base(gitRepository, gitDir, workTreeDir)
    {
        _repositories = new Lazy<GitReferenceRepository[]>(() => GetRepositories().ToArray());
    }

    private readonly Lazy<GitReferenceRepository[]> _repositories;

    private IEnumerable<GitReferenceRepository> GetRepositories()
    {
        if (File.Exists(Path.Combine(GitDir, "refs", "heads")))
        {
            yield return new GitShellReferenceRepository(this, GitDir, WorkTreeDir);
            yield break;
        }

        yield return new GitFileReferenceRepository(this, GitDir, WorkTreeDir);

        if (File.Exists(Path.Combine(GitDir, GitPackedRefsReferenceRepository.PackedRefsFile)))
            yield return new GitPackedRefsReferenceRepository(this, GitDir, WorkTreeDir);
    }

    public override async IAsyncEnumerable<GitReference> GetAll(HashSet<string> alreadyReturned)
    {
        foreach (var v in Sources)
        {
            await foreach (var r in v.GetAll(alreadyReturned).ConfigureAwait(false))
            {
                if (!alreadyReturned.Contains(r.Name))
                {
                    alreadyReturned.Add(r.Name);

                    switch (r.Name)
                    {
                        case "refs/stash":
                            continue;
                    }

                    yield return r;
                }
            }
        }
    }

    public override IAsyncEnumerable<GitReferenceChange> GetChanges(GitReference reference)
    {
        foreach (var v in Sources)
        {
            var r = v.GetChanges(reference);

            if (r is not null)
                return r;
        }

        return AsyncEnumerable.Empty<GitReferenceChange>();
    }

    protected internal override async ValueTask<GitReference?> ResolveAsync(GitReference gitReference)
    {
        foreach (var v in Sources)
        {
            var r = await v.ResolveAsync(gitReference).ConfigureAwait(false);

            if (r is not null)
                return r;
        }

        return null;
    }

    public override async ValueTask<IEnumerable<GitReference>> ResolveByOidAsync(GitId id, HashSet<string> processed)
    {
        HashSet<GitReference>? references = null;
        foreach (var v in Sources)
        {
            var rs = await v.ResolveByOidAsync(id, processed).ConfigureAwait(false);

            if (rs != null)
            {

                foreach (var r in rs)
                {
                    references ??= new();
                    references.Add(r);
                }
            }
        }

        return references ?? Enumerable.Empty<GitReference>();
    }

    protected internal override async ValueTask<GitReference?> GetUnsafeAsync(string name)
    {
        foreach (var v in Sources)
        {
            var r = await v.GetUnsafeAsync(name).ConfigureAwait(false);

            if (r is not null)
                return r;
        }

        return null;
    }

    protected GitReferenceRepository[] Sources => _repositories.Value;
}
