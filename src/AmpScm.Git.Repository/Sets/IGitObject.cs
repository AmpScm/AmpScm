namespace AmpScm.Git.Sets;

public interface IGitObject
{
    ValueTask ReadAsync();
}

public interface IGitNamedObject : IGitObject
{
    public string Name { get; }
}
