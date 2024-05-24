namespace AmpScm.Git.Client.Porcelain;

public enum GitStashCommand
{
    Push,
    Pop
}

public class GitStashArgs : GitPorcelainArgs
{
    public GitStashCommand Command { get; set; }
    public override void Verify()
    {

    }
}

public partial class GitPorcelain
{
    [GitCommand("stash")]
    public static async ValueTask Stash(this GitPorcelainClient c, GitStashArgs? options = null)
    {
        options?.Verify();
        options ??= new();

        List<string> args = new();

        switch (options.Command)
        {
            case GitStashCommand.Push:
                args.Add("push");
                break;
        }

        await c.Repository.RunGitCommandAsync("stash", args).ConfigureAwait(false);
    }
}
