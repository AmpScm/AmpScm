using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Git.Objects;
using AmpScm.Git.Implementation;

namespace AmpScm.Git.Sets
{
    public class GitTreeItemCollection : GitSet, IEnumerable<GitTreeItem>, IAsyncEnumerable<GitTreeItem>
    {
        readonly GitTree _gitTree;
        readonly bool _justFiles;

        internal GitTreeItemCollection(GitTree gitTree, bool justFiles)
            : base(gitTree?.Repository ?? throw new ArgumentNullException(nameof(gitTree)))
        {
            if (gitTree is null)
                throw new ArgumentNullException(nameof(gitTree));

            _gitTree = gitTree;
            _justFiles = justFiles;
        }

        public async IAsyncEnumerator<GitTreeItem> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            Stack<(IAsyncEnumerator<GitTreeEntry>, string)>? inside = null;

            IAsyncEnumerator<GitTreeEntry> cur = _gitTree.GetAsyncEnumerator(cancellationToken);
            string path = "";

            do
            {
                while (await cur.MoveNextAsync().ConfigureAwait(false))
                {
                    var c = cur.Current;

                    if (c is GitDirectoryTreeEntry dir)
                    {
                        if (!_justFiles)
                            yield return new GitTreeItem(path + c.Name, c);

                        inside ??= new Stack<(IAsyncEnumerator<GitTreeEntry>, string)>();

                        await dir.ReadAsync().ConfigureAwait(false);

                        var t = dir.Tree?.GetAsyncEnumerator(cancellationToken);

                        if (t != null)
                        {
                            inside.Push((cur, path));

                            path += dir.EntryName;
                            cur = t;
                        }
                    }
                    else
                    {
                        yield return new GitTreeItem(path + c.Name, c);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }

                await cur.DisposeAsync().ConfigureAwait(false);

                if (inside?.Count > 0)
                {
                    (cur, path) = inside.Pop();
                }
                else
                    break;
            }
            while (cur != null);
        }

        public bool TryGet(string path, [NotNullWhen(true)] out GitTreeItem? item)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            GitTree tree = _gitTree;

            int n = -1;
            do
            {
                int start = n + 1;
                n = path.IndexOf('/', start);
                if (n < 0)
                    n = path.Length;
                else if (n == start)
                {
                    item = null;
                    return false;
                }

                string sp = path.Substring(start, n - start);
                bool found = false;
                foreach(var i in tree)
                {
                    if (i.Name == sp)
                    {
                        if (n == path.Length && (!_justFiles || i.ElementType.IsFile()))
                        {
                            item = new GitTreeItem(path, i);
                            return true;
                        }

                        if (i.ElementType == GitTreeElementType.Directory)
                        {
                            tree = (GitTree)i.GitObject;
                        }
                        else if (i.ElementType == GitTreeElementType.GitCommitLink)
                        {
                            tree = ((GitCommit)i.GitObject).Tree;
                        }
                        else
                        {
                            item = null;
                            return false;
                        }
                        found = true;
                        break;
                    }
                }

                if (!found)
                    break;
            }
            while (n < path.Length);

            item = null;
            return false;
        }

        public GitTreeItem this[string path]
        {
            get
            {
                if (TryGet(path, out var item))
                    return item.Value;

                throw new ArgumentOutOfRangeException(nameof(path), $"Item with path '{path}' does not exist");
            }
        }

        public IEnumerator<GitTreeItem> GetEnumerator()
        {
            return this.AsNonAsyncEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public readonly struct GitTreeItem : IEquatable<GitTreeItem>
    {
        public GitTreeItem(string path, GitTreeEntry entry) : this()
        {
            Path = path;
            Entry = entry;
        }

        public string Name => Entry.Name;
        public string EntryName => Entry.EntryName;
        public string Path { get; }
        public GitTreeEntry Entry { get; }

        public override bool Equals(object? obj)
        {
            return (obj is GitTreeItem other) && Equals(other);
        }

        public bool Equals(GitTreeItem other)
        {
            return Entry.Equals(other.Entry);
        }

        public override int GetHashCode()
        {
            return Entry.GetHashCode();
        }

        public static bool operator ==(GitTreeItem left, GitTreeItem right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GitTreeItem left, GitTreeItem right)
        {
            return !(left == right);
        }
    }
}
