using AmpScm.Git.Sets;

namespace AmpScm.Git;

public sealed class GitStash : IGitObject
{
    private readonly GitReferenceChange _change;

    internal GitStash(GitReferenceChange change)
    {
        _change = change ?? throw new ArgumentNullException(nameof(change));
    }

    public string Message => (_change.TargetObject as GitCommit)?.Message ?? "";

    public string Reason => _change.Reason;

    public async ValueTask ReadAsync()
    {
        await ((IGitObject)_change).ReadAsync().ConfigureAwait(false);
    }
}
