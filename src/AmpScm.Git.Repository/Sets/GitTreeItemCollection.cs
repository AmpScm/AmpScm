using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Git.Implementation;

namespace AmpScm.Git.Sets
{
    public class GitTreeItemCollection : IEnumerable<GitTreeItem>, IAsyncEnumerable<GitTreeItem>
    {
        readonly GitTree _gitTree;
        readonly bool _justFiles;

        internal GitTreeItemCollection(GitTree gitTree, bool justFiles)
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

        public IEnumerator<GitTreeItem> GetEnumerator()
        {
            return this.AsNonAsyncEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public struct GitTreeItem : IEquatable<GitTreeItem>
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
