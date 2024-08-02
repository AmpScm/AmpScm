using System.Collections;
using AmpScm.Buckets;
using AmpScm.Buckets.Git.Objects;

namespace AmpScm.Git.Objects;

public sealed class GitTreeWriter : GitObjectWriter<GitTree>, IEnumerable<KeyValuePair<string, IGitLazy<GitObject>>>
{
    private readonly SortedList<string, TWItem> _items = new SortedList<string, TWItem>(StringComparer.Ordinal);

    public override GitObjectType Type => GitObjectType.Tree;

    public GitTreeWriter()
    {

    }

    private static bool IsValidName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        else if (name == ".")
            return false;
        else if (name.Contains('/', StringComparison.Ordinal))
            return false;

        return true;
    }

    public void Add<TGitObject>(string name, IGitLazy<TGitObject> item, GitTreeElementType? setType = null)
        where TGitObject : GitObject
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));
        else if (item is null)
            throw new ArgumentNullException(nameof(item));

        if (typeof(TGitObject) == typeof(GitObject))
        {
            typeof(GitTreeWriter).GetMethod(nameof(Add))!.MakeGenericMethod(item.Type.AsType()).Invoke(this, new object?[] { name, item, setType });
            return;
        }

        if (IsValidName(name))
        {
            if (_items.ContainsKey(name))
                throw new ArgumentOutOfRangeException(nameof(name), $"Entry with name '{name}' already exists");

            _items.Add(name, new TWItem<TGitObject>(name, item, setType));
        }
        else if (name.Contains('/', StringComparison.Ordinal))
        {
            var p = name.Split('/');
            GitTreeWriter tw = this;

            foreach (var si in p.Take(p.Length - 1))
            {
                if (tw._items.TryGetValue(si, out var v))
                {
                    if (v.Writer is GitTreeWriter subTw)
                        tw = subTw;
                    else if (v.Lazy is GitTree)
                        tw = (GitTreeWriter)v.EnsureWriter();
                    else
                        throw new InvalidOperationException();

                    tw.Updated();
                }
                else
                {
                    var stw = CreateEmpty();
                    tw.Add(si, stw, setType: null);
                    tw = stw;
                }
            }

            tw.Add(p.Last(), item, setType);
        }
        else
            throw new ArgumentOutOfRangeException(nameof(name), name, "Invalid name");

        Id = null;
    }

    private void Updated()
    {
        Id = null;
    }


    public IGitLazy<GitObject> this[string key]
    {
        get => _items[key].Lazy;
        set => Add(key, value);
    }

    public void Replace<TGitObject>(string name, IGitLazy<TGitObject> item, GitTreeElementType? setType = null)
        where TGitObject : GitObject
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));
        else if (item is null)
            throw new ArgumentNullException(nameof(item));

        if (typeof(TGitObject) == typeof(GitObject))
        {
            typeof(GitTreeWriter).GetMethod(nameof(Replace))!.MakeGenericMethod(item.Type.AsType()).Invoke(this, new object?[] { name, item, setType });
            return;
        }

        if (IsValidName(name))
        {
            if (_items.ContainsKey(name))
                throw new ArgumentOutOfRangeException(nameof(name));

            _items[name] = new TWItem<TGitObject>(name, item, setType);
        }
        else if (name.Contains('/', StringComparison.Ordinal))
        {
            var p = name.Split('/');
            GitTreeWriter tw = this;

            foreach (var si in p.Take(p.Length - 1))
            {
                if (tw._items.TryGetValue(si, out var v))
                {
                    if (v.Writer is GitTreeWriter subTw)
                        tw = subTw;
                    else if (v.Lazy is GitTree)
                        tw = (GitTreeWriter)v.EnsureWriter();
                    else
                        throw new InvalidOperationException();

                    tw.Updated();
                }
                else
                {
                    var stw = CreateEmpty();
                    tw.Add(si, stw);
                    tw = stw;
                }
            }

            tw.Replace(p.Last(), item, setType);
        }
        else
            throw new ArgumentOutOfRangeException(nameof(name), name, "Invalid name");

        Id = null;
    }

    public bool Remove(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        if (IsValidName(name))
        {
            if (_items.Remove(name))
            {
                Updated();
                return true;
            }
            return false;
        }
        else if (name.Contains('/', StringComparison.Ordinal))
        {
            var p = name.Split('/');
            GitTreeWriter tw = this;

            foreach (var si in p.Take(p.Length - 1))
            {
                if (tw._items.TryGetValue(si, out var v))
                {
                    if (v.Writer is GitTreeWriter subTw)
                        tw = subTw;
                    else if (v.Lazy is GitTree)
                        tw = (GitTreeWriter)v.EnsureWriter();
                    else
                        throw new InvalidOperationException();

                    tw.Updated();
                }
                else
                    return false;
            }

            return tw.Remove(p.Last());
        }
        else
            throw new ArgumentOutOfRangeException(nameof(name), name, "Invalid name");
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
            Bucket b = _items.Values.Select(x =>
                new GitTreeElementRecord()
                {
                    Name = x.Name,
                    Type = x.Type,
                    Id = x.Lazy.Id ?? throw new InvalidOperationException("Id not set on entry")
                }.AsBucket()).AsBucket();
#pragma warning restore CA2000 // Dispose objects before losing scope

            Id = await WriteBucketAsObject(b, repository).ConfigureAwait(false);
        }

        return Id;
    }

    private abstract class TWItem
    {
        protected TWItem(string name)
        {
            Name = name;
        }
        public string Name { get; private set; }
        public GitTreeElementType Type { get; internal set; }

        public abstract ValueTask EnsureAsync(GitRepository repository);

        public abstract GitObjectWriter EnsureWriter();

        public abstract GitObjectWriter? Writer { get; }

        public abstract IGitLazy<GitObject> Lazy { get; }
    }

    private sealed class TWItem<TGitObject> : TWItem
        where TGitObject : GitObject
    {
        private IGitLazy<TGitObject> _lazy;
        private GitObjectWriter? _writer;

        public TWItem(string name, IGitLazy<TGitObject> lazy, GitTreeElementType? setType)
            : base(name)
        {
            if (typeof(TGitObject) == typeof(GitObject))
                throw new InvalidOperationException();

            _lazy = lazy;
            if (lazy is TGitObject item)
            {
                Type = item.Type switch
                {
                    GitObjectType.Blob => GitTreeElementType.File,
                    GitObjectType.Tree => GitTreeElementType.Directory,
                    GitObjectType.Commit => GitTreeElementType.GitCommitLink,
                    _ or GitObjectType.None or GitObjectType.Tag => GitTreeElementType.None
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
                    _ or GitObjectType.None or GitObjectType.Tag => GitTreeElementType.None
                };
            }
            else
                throw new InvalidOperationException();

            if (setType != null)
            {
                switch (setType.Value)
                {
                    case GitTreeElementType.File:
                    case GitTreeElementType.FileExecutable:
                    case GitTreeElementType.SymbolicLink:
                        if (typeof(TGitObject) == typeof(GitBlob))
                            Type = setType.Value;
                        else
                            throw new ArgumentOutOfRangeException(nameof(setType));
                        break;
                    default:
                    case GitTreeElementType.None:
                    case GitTreeElementType.Directory:
                    case GitTreeElementType.GitCommitLink:
                        if (Type != setType.Value)
                            throw new ArgumentOutOfRangeException(nameof(setType));
                        break;
                }
            }
        }

        public override GitObjectWriter? Writer => _writer;

        public override IGitLazy<GitObject> Lazy => _lazy;

        public override async ValueTask EnsureAsync(GitRepository repository)
        {
            await _lazy.WriteToAsync(repository).ConfigureAwait(false);
        }

        public override GitObjectWriter EnsureWriter()
        {
            if (_writer is null)
            {
                _writer = ((GitObject)Lazy).AsWriter();
                _lazy = (GitObjectWriter<TGitObject>)_writer;
            }

            return _writer;
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
