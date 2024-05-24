namespace AmpScm.Git.Client.Plumbing;

public abstract class GitPlumbingArgs
{
    public abstract void Verify();
}


public static partial class GitPlumbing
{
    public static GitPlumbingClient GetPlumbing(this GitRepository repository)
    {
        return new GitPlumbingClient(repository);
    }

    public static async ValueTask<(int ExitCode, string OutputText)> RunRawCommand(this GitPlumbingClient c, string command, params string[] args)
    {
        return await c.Repository.RunGitCommandOutAsync(command, args).ConfigureAwait(false);
    }
}
