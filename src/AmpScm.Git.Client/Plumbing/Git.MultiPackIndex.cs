namespace AmpScm.Git.Client.Plumbing;


public enum GitMultiPackIndexCommand
{
    Write,
    Verify,
    Repack,
}
public class GitMultiPackIndexArgs : GitPlumbingArgs
{
    public GitMultiPackIndexCommand Command { get; set; } // =Write

    public bool? Bitmap { get; set; }
    public override void Verify()
    {

    }
}

public partial class GitPlumbing
{
    [GitCommand("multi-pack-index")]
    public static async ValueTask MultiPackIndex(this GitPlumbingClient c, GitMultiPackIndexArgs? options = null)
    {
        options?.Verify();

        options ??= new();

        List<string> args = new();

        args.Add(options.Command switch
        {
            GitMultiPackIndexCommand.Write => "write",
            GitMultiPackIndexCommand.Verify => "verify",
            GitMultiPackIndexCommand.Repack => "repack",
            _ => throw new ArgumentException()
        });

        // Bitmap is nullable. Handle the two explicit states
        if (options.Bitmap == true)
            args.Add("--bitmap");
        else if (options.Bitmap == false)
            args.Add("--no-bitmap");

        await c.Repository.RunGitCommandAsync("multi-pack-index", args);

        Porcelain.GitPorcelain.RemoveReadOnlyIfNecessary(c.Repository.GitDirectory);
    }
}
