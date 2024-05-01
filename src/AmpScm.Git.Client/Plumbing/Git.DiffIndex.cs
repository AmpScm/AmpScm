namespace AmpScm.Git.Client.Plumbing;

public class GitDiffIndexArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("diff-index")]
    public static async ValueTask DiffIndex(this GitPlumbingClient c, GitDiffIndexArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
