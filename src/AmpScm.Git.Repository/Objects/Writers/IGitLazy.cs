namespace AmpScm.Git.Objects;

public interface IGitLazy<out TGitObject>
    where TGitObject : GitObject
{
    GitId? Id { get; }

    GitObjectType Type { get; }

    ValueTask<GitId> WriteToAsync(GitRepository repository);
}
