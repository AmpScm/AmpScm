namespace AmpScm.Git.Client.Plumbing;

public class GitMakeTreeArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("mktree")]
    public static async ValueTask MakeTree(this GitPlumbingClient c, GitMakeTreeArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
