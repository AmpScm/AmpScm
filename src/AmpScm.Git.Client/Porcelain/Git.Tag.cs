namespace AmpScm.Git.Client.Porcelain;

public class GitTagArgs : GitPorcelainArgs
{
    public string? Message { get; set; }
    public bool Sign { get; set; }

    public override void Verify()
    {
    }
}

public partial class GitPorcelain
{
    [GitCommand("tag")]
    public static async ValueTask Tag(this GitPorcelainClient c, string name, GitTagArgs? options = null)
    {
        options?.Verify();
        options ??= new();

        List<string> args = new();

        if (!string.IsNullOrEmpty(options.Message))
        {
            args.Add("-m");
            args.Add(options.Message!);
        }

        if (options.Sign)
            args.Add("-s");

        args.Add("--");
        args.Add(name);

        await c.Repository.RunGitCommandAsync("tag", args);
    }
}
