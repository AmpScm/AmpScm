namespace AmpScm.Git.Client.Porcelain;

public class GitPorcelainClient
{
    public GitPorcelainClient(GitRepository repository)
    {
        Repository = repository;
    }

    internal GitRepository Repository { get; }

    internal ValueTask ThrowNotImplemented()
    {
        throw new NotImplementedException();
    }
}
