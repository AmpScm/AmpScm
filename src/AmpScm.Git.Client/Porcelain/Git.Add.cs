namespace AmpScm.Git.Client.Porcelain;

public class GitAddArgs : GitPorcelainArgs
{
    public override void Verify()
    {

    }
}

public partial class GitPorcelain
{
    [GitCommand("add")]
    public static ValueTask Add(this GitPorcelainClient c, string path, GitAddArgs? options=null)
    {
        return Add(c, new[] { path }, options);
    }

    [GitCommand("add")]
    public static async ValueTask Add(this GitPorcelainClient c, string[] paths, GitAddArgs? options = null)
    {
        options?.Verify();
        options ??= new();

        List<string> args = new List<string>();

        args.Add("--");
        args.AddRange(paths);

        await c.Repository.RunGitCommandAsync("add", args).ConfigureAwait(false);

        RemoveReadOnlyIfNecessary(c.Repository.GitDirectory);
    }
}
