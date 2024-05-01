using AmpScm.Git.Sets;

namespace AmpScm.Git.Implementation;

// Internal marker interface
internal interface IGitQueryRoot
{
    IQueryable<TResult> GetAll<TResult>()
        where TResult : GitObject;

    IQueryable<TResult> GetAllNamed<TResult>()
        where TResult : class, IGitNamedObject;

    ValueTask<TResult?> GetByIdAsync<TResult>(GitId id)
        where TResult : GitObject;

    ValueTask<TResult?> GetNamedAsync<TResult>(string name)
        where TResult : class, IGitNamedObject;
    IQueryable<GitRevision> GetRevisions(GitRevisionSet set);
    IQueryable<GitReferenceChange> GetAllReferenceChanges(GitReferenceChangeSet set);
    IQueryable<GitStash> GetAllStashes(GitStashSet set);
}
