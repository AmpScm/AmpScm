using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Git.Objects;

namespace AmpScm.Git.Objects
{
    public sealed class GitTreeWriter : GitObjectWriter<GitTree>, IEnumerable<KeyValuePair<string, IGitLazy<GitObject>>>
    {
        readonly SortedList<string, Item> _items = new SortedList<string, Item>(StringComparer.Ordinal);

        public override GitObjectType Type => GitObjectType.Tree;

        private GitTreeWriter()
        {

        }

        static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            else if (name == ".")
                return false;
            else if (name.Contains('/', StringComparison.Ordinal))
                return false;

            return true;
        }

        public void Add<TGitObject>(string name, IGitLazy<TGitObject> item)
            where TGitObject : GitObject
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            else if (item is null)
                throw new ArgumentNullException(nameof(item));

            if (IsValidName(name))
            {
                if (_items.ContainsKey(name))
                    throw new ArgumentOutOfRangeException(nameof(name));

                _items.Add(name, new Item<TGitObject>(name, item));
            }
            else if (name.Contains('/', StringComparison.Ordinal))
            {
                var p = name.Split('/');
                GitTreeWriter tw = this;

                foreach (var si in p.Take(p.Length - 1))
                {
                    if (tw._items.TryGetValue(si, out var v)
                        && v.Writer is GitTreeWriter subTw)
                    {
                        tw = subTw;
                    }
                    else
                    {
                        tw.Add(si, subTw = GitTreeWriter.CreateEmpty());
                        tw = subTw;
                    }
                }

                tw.Add(p.Last(), item);
            }
            else
                throw new ArgumentOutOfRangeException(nameof(name), name, "Invalid name");

            Id = null;
        }

        public void Replace<TGitObject>(string name, IGitLazy<TGitObject> item)
            where TGitObject : GitObject
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            else if (item is null)
                throw new ArgumentNullException(nameof(item));

            if (IsValidName(name))
            {
                if (_items.ContainsKey(name))
                    throw new ArgumentOutOfRangeException(nameof(name));

                _items[name] = new Item<TGitObject>(name, item);
            }
            else if (name.Contains('/', StringComparison.Ordinal))
            {
                var p = name.Split('/');
                GitTreeWriter tw = this;

                foreach (var si in p.Take(p.Length - 1))
                {
                    if (tw._items.TryGetValue(si, out var v)
                        && v.Writer is GitTreeWriter subTw)
                    {
                        tw = subTw;
                    }
                    else
                    {
                        tw.Add(si, subTw = GitTreeWriter.CreateEmpty());
                        tw = subTw;
                    }
                }

                tw.Replace(p.Last(), item);
            }
            else
                throw new ArgumentOutOfRangeException(nameof(name), name, "Invalid name");

            Id = null;
        }

        public override async ValueTask<GitId> WriteToAsync(GitRepository repository)
        {
            if (repository is null)
                throw new ArgumentNullException(nameof(repository));

            if (Id is null || !repository.Trees.ContainsId(Id))
            {
                foreach (var i in _items.Values)
                {
                    await i.EnsureAsync(repository).ConfigureAwait(false);
                }

#pragma warning disable CA2000 // Dispose objects before losing scope
                Bucket b = new AggregateBucket(_items.Values.Select(x =>
                    new GitTreeElementRecord()
                    {
                        Name = x.Name,
                        Type = x.Type,
                        Id = x.Lazy.Id ?? throw new InvalidOperationException("Id not set on entry")
                    }.AsBucket()).ToArray());
#pragma warning restore CA2000 // Dispose objects before losing scope

                Id = await WriteBucketAsObject(b, repository).ConfigureAwait(false);
            }

            return Id;
        }

        abstract class Item
        {
            protected Item(string name)
            {
                Name = name;
            }
            public string Name { get; private set; }
            public GitTreeElementType Type { get; internal set; }

            public abstract ValueTask EnsureAsync(GitRepository repository);

            public abstract GitObjectWriter? Writer { get; }

            public abstract IGitLazy<GitObject> Lazy { get; }
        }

        sealed class Item<TGitObject> : Item
            where TGitObject : GitObject
        {
            IGitLazy<TGitObject> _lazy;
            private GitObjectWriter? _writer;

            public Item(string name, IGitLazy<TGitObject> lazy)
                : base(name)
            {
                _lazy = lazy;
                if (lazy is TGitObject item)
                {
                    Type = item.Type switch
                    {
                        GitObjectType.Blob => GitTreeElementType.File,
                        GitObjectType.Tree => GitTreeElementType.Directory,
                        GitObjectType.Commit => GitTreeElementType.GitCommitLink,
                        _ => GitTreeElementType.None
                    };
                }
                else if (lazy is GitObjectWriter writer)
                {
                    _writer = writer;
                    Type = writer.Type switch
                    {
                        GitObjectType.Blob => GitTreeElementType.File,
                        GitObjectType.Tree => GitTreeElementType.Directory,
                        GitObjectType.Commit => GitTreeElementType.GitCommitLink,
                        _ => GitTreeElementType.None
                    };
                }
                else
                    throw new InvalidOperationException();
            }

            public override GitObjectWriter? Writer => _writer;

            public override IGitLazy<GitObject> Lazy => _lazy;

            public override async ValueTask EnsureAsync(GitRepository repository)
            {
                await _lazy.WriteToAsync(repository).ConfigureAwait(false);
            }
        }

        public static GitTreeWriter CreateEmpty()
        {
            return new GitTreeWriter();
        }

        public IEnumerator<KeyValuePair<string, IGitLazy<GitObject>>> GetEnumerator()
        {
            foreach (var i in _items)
            {
                yield return new KeyValuePair<string, IGitLazy<GitObject>>(i.Key, i.Value.Lazy);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
