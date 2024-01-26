using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Sets;

public enum GitChange
{
    None,
    Added,
    Modified,
    Deleted
}

public sealed class GitChangedPath : IEquatable<GitChangedPath>
{
    internal GitChangedPath(string path, GitChange change)
    {
        Path = path;
        Change = change;
    }

    public string Path { get; }
    public GitChange Change { get; }

    public bool Equals(GitChangedPath? other)
    {
        if (other is null)
            return false;

        return Path == other.Path && Change == other.Change;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as GitChangedPath);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Path);
    }
}
