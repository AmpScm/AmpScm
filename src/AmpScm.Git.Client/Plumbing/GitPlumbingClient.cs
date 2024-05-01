namespace AmpScm.Git.Client.Plumbing;

public class GitPlumbingClient
{
    public GitPlumbingClient(GitRepository repository)
    {
        Repository = repository;
    }

    internal GitRepository Repository { get; }

    internal ValueTask ThrowNotImplemented()
    {
        throw new NotImplementedException();
    }
}
