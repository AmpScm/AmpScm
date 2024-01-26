using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git.Sets;

namespace AmpScm.Git;

public class GitRevision :IGitObject
{
    internal GitRevision(GitCommit commit)
    {
        Commit = commit;
    }

    public GitId Id => Commit.Id;

    /// <summary>
    /// The original (unmodified) commit backing this revision. Simplified values are stored on this <see cref="GitRevision"/>
    /// </summary>
    public GitCommit Commit { get; }

    public DateTimeOffset CommitTime => Committer.When; // TODO: Optimize

    public IReadOnlyList<GitId> ParentIds => Commit.ParentIds; // May be tweaked when simplifying

    public GitSignature Author => Commit.Author;
    public GitSignature Committer => Commit.Committer;

    public string Message => Commit.Message;
    public string Summary => Commit.Summary;

#pragma warning disable CA1822 // Mark members as static
    public IEnumerable<GitChangedPath> ChangedPaths => Enumerable.Empty<GitChangedPath>();
#pragma warning restore CA1822 // Mark members as static

    public ValueTask ReadAsync()
    {
        return default;
    }
}
