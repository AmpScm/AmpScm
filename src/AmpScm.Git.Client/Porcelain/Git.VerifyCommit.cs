namespace AmpScm.Git.Client.Porcelain;

public class GitVerifyCommitArgs : GitPorcelainArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPorcelain
{
    [GitCommand("verify-commit")]
    public static async ValueTask VerifyCommit(this GitPorcelainClient c, string commit, GitVerifyCommitArgs? options = null)
    {
        options?.Verify();

        options ??= new();

        List<string> args = new();

        args.Add(commit);

        await c.Repository.RunGitCommandAsync("verify-commit", args).ConfigureAwait(false);
    }
}
