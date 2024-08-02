using System.Diagnostics;
using AmpScm.Buckets.Git;
using AmpScm.Git.Sets;

namespace AmpScm.Git;

[DebuggerDisplay("{Type} {Id.ToString(\"x12\"),nq}")]
public abstract class GitObject : IEquatable<GitObject>, IGitObject
{
    protected internal GitRepository Repository { get; }
    public GitId Id { get; }

    public abstract GitObjectType Type { get; }

    private protected GitObject(GitRepository repository, GitId id)
    {
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        Id = id ?? throw new ArgumentNullException(nameof(id));
    }

    internal static async ValueTask<GitObject> FromBucketAsync(GitRepository repository, GitObjectBucket rdr, GitId id, GitObjectType type = GitObjectType.None)
    {
        GitObjectType tp;

        if (type == GitObjectType.None)
            tp = await rdr.ReadTypeAsync().ConfigureAwait(false);
        else
            tp = type;

        switch (tp)
        {
            case GitObjectType.Commit:
                return new GitCommit(repository, rdr, id);
            case GitObjectType.Tree:
                return new GitTree(repository, rdr, id);
            case GitObjectType.Blob:
                return new GitBlob(repository, rdr, id);
            case GitObjectType.Tag:
                return new GitTagObject(repository, rdr, id);
            default:
            case GitObjectType.None:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    public virtual ValueTask ReadAsync()
    {
        return default;
    }

    public bool Equals(GitObject? other)
    {
        return other?.Id.Equals(Id) ?? false;
    }

    public sealed override bool Equals(object? obj)
    {
        return Equals(obj as GitObject);
    }

    public sealed override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(GitObject? one, GitObject? other)
    {
        return one?.Equals(other) ?? (other is null);
    }

    public static bool operator !=(GitObject? one, GitObject? other)
    {
        return !(one?.Equals(other) ?? (other is null));
    }
}
