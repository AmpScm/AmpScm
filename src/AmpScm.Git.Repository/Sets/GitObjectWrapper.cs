namespace AmpScm.Git.Sets;

public class GitObjectWrapper<TGitObject> : IGitObject
    where TGitObject : GitObject
{
    protected virtual TGitObject GitObject { get; }

    private protected GitObjectWrapper(TGitObject obj)
    {
        this.GitObject = obj;
    }
    public virtual ValueTask ReadAsync()
    {
        return default;
    }
}

public class GitNamedObjectWrapper<TGitObject, TNamedObject> : GitObjectWrapper<TGitObject>, IGitNamedObject
    where TGitObject : GitObject
    where TNamedObject : class, IGitNamedObject
{

    protected virtual TNamedObject Named { get; }

    private protected GitNamedObjectWrapper(TNamedObject named, TGitObject? obj)
        : base(obj!)
    {
        Named = named;
    }

    public virtual string Name => Named.Name;
}
