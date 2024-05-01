namespace AmpScm.Git.Client;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class GitCommandAttribute : Attribute
{
    public GitCommandAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
