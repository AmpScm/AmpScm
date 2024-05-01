using System.Diagnostics;
using AmpScm.Git.References;
using AmpScm.Git.Sets;

namespace AmpScm.Git;

[DebuggerDisplay("{ShortName,nq}: Target={GitObject}, Name={Name,nq}")]
public class GitReference : IGitNamedObject, IEquatable<GitReference>
{
    protected GitReferenceRepository ReferenceRepository { get; }

    private object? _object;
    private object? _commit;
    private Func<ValueTask<GitId?>>? _resolver;
    private string? _shortName;
    private GitReference? _resolved;

    internal GitReference(GitReferenceRepository repository, string name, Func<ValueTask<GitId?>> resolver)
        : this(repository, name)
    {
        if (resolver is null)
            throw new ArgumentNullException(nameof(resolver));

        _resolver = resolver;
    }

    internal GitReference(GitReferenceRepository repository, string name, GitId value)
        : this(repository, name)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));

        _object = value;
    }

    private protected GitReference(GitReferenceRepository repository, string name)
    {
        ReferenceRepository = repository ?? throw new ArgumentNullException(nameof(repository));
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string Name { get; }

    public string ShortName
    {
        get
        {
            if (_shortName == null)
            {
                if (Name.StartsWith("refs/heads/", StringComparison.Ordinal))
                    _shortName = Name.Substring("refs/heads/".Length);
                else if (Name.StartsWith("refs/remotes/", StringComparison.Ordinal))
                    _shortName = Name.Substring("refs/remotes/".Length);
                else if (Name.StartsWith("refs/tags/", StringComparison.Ordinal))
                    _shortName = Name.Substring("refs/tags/".Length);
                else if (Name.StartsWith("refs/", StringComparison.Ordinal))
                    _shortName = Name.Substring("refs".Length);
                else
                    _shortName = Name;
            }

            return _shortName;
        }
    }

    public virtual async ValueTask ReadAsync()
    {
        if (_object is null)
        {
            if (_resolver is not null)
            {
                _object = await _resolver().ConfigureAwait(false);
                _resolver = null;
            }
            _object ??= await ReferenceRepository.Repository.References.GetAsync(Name).ConfigureAwait(false);

            if (_object is GitReference gr)
            {
                await gr.ReadAsync().ConfigureAwait(false);
                _object = gr._object;
            }
        }

        if (_object is GitId oid)
        {
            _object = await ReferenceRepository.Repository.GetAsync<GitObject>(oid).ConfigureAwait(false) ?? _object;
        }
    }

    public virtual GitObject? GitObject
    {
        get
        {
            if (_object is not Git.GitObject)
                ReadAsync().AsTask().Wait();

            return _object as GitObject;
        }
    }

    public virtual GitId? Id
    {
        get
        {
            if (_object == null)
                ReadAsync().AsTask().Wait();

            if (_object is GitId oid)
                return oid;
            else if (_object is GitObject ob)
                return ob.Id;
            else
                return null;
        }
    }


    public virtual GitCommit? Commit
    {
        get
        {
            if (_commit is GitCommit commit)
                return commit;
            else if (_object is GitTagObject tag)
            {
                if (tag.GitObject is GitCommit c2)
                {
                    _commit = c2;
                    return c2;
                }
            }
            else if (_object is GitCommit c3)
            {
                _commit = c3;
                return c3;
            }
            else if (_commit is GitId oid)
            {
                c3 = ReferenceRepository.Repository.GetAsync<GitCommit>(oid).AsTask().Result!;
                _commit = c3;
                return c3;
            }
            else if (GitObject is GitObject ob)
            {
                if (ob is GitCommit c4)
                {
                    _commit = c4;
                    return c4;
                }
                else if (ob is GitTagObject tag2)
                {
                    c4 = (tag2.GitObject as GitCommit)!;
                    _commit = c4;
                    return c4;
                }
            }
            return null;
        }
    }

    public GitTree? Tree => Commit?.Tree;


    public GitRevisionSet Revisions => new GitRevisionSet(ReferenceRepository.Repository).AddReference(this);

    public GitReferenceChangeSet ReferenceChanges => new GitReferenceChangeSet(ReferenceRepository.Repository, this);

    // HEAD is only a branch if it is detached
    internal bool IsBranch => Name.StartsWith("refs/heads/", StringComparison.Ordinal) || (Name == GitReferenceRepository.Head && ((this as GitSymbolicReference)?.Reference == this));
    internal bool IsTag => Name.StartsWith("refs/tags/", StringComparison.Ordinal);

    private static HashSet<char> InvalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());

    public static bool ValidName(string name, bool allowSpecialSymbols)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        char last = '\0';

        for (int i = 0; i < name.Length; last = name[i++])
        {
            if (char.IsLetterOrDigit(name, i))
                continue;
            switch (name[i])
            {
                case '\\':
                case '~':
                case ':':
                case '^':
                case '[':
                    return false;
                case '.' when (last == '/' || last == '\0'):
                    return false;
                case '/' when (last == '\0'):
                    return false;
                case '{' when last == '@':
                    return false;
                case '@' when (last == '/' || last == '\0'):
                    if (("/" + name + "/").Contains("/@/", StringComparison.Ordinal))
                        return false;
                    continue;
                case '.' when (i + 1 < name.Length && name[i + 1] == '.'):
                    return false;
                case '/':
                case '.':
                    continue;
                default:
                    if (char.IsControl(name, i) || char.IsWhiteSpace(name, i) || InvalidChars.Contains(name[i]))
                        return false;
                    break;
            }
        }
        return (name.Length > 1) && (name.Contains('/', StringComparison.Ordinal) || (allowSpecialSymbols && AllUpper(name))) && !name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase);
    }

    internal GitReference SetPeeled(GitId? peeled)
    {
        _commit = peeled;
        return this;
    }

    internal static bool AllUpper(string name)
    {
        for (int i = 0; i < name.Length; i++)
        {
            if (!char.IsUpper(name, i) && name[i] != '_')
                return false;
        }
        return true;
    }

    public async ValueTask<GitReference> ResolveAsync()
    {
        _resolved ??= (await ReferenceRepository.ResolveAsync(this).ConfigureAwait(false)) ?? this;

        return _resolved;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as GitReference);
    }

    public bool Equals(GitReference? other)
    {
        if (other is null)
            return false;

        return Name == other.Name && other.ReferenceRepository?.Repository == ReferenceRepository?.Repository;
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode(StringComparison.Ordinal);
    }

    public GitReference Resolved
    {
        get => ResolveAsync().AsTask().Result;
    }

    public static bool operator ==(GitReference? r1, GitReference? r2)
        => r1?.Equals(r2) ?? false;

    public static bool operator !=(GitReference? r1, GitReference? r2)
    {
        return !(r1 == r2);
    }

    public override string ToString()
    {
        return $"{Name}: {Id}";
    }
}

