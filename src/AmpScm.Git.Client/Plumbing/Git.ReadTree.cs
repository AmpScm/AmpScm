namespace AmpScm.Git.Client.Plumbing;

public class GitReadTreeArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("read-tree")]
    public static async ValueTask ReadTree(this GitPlumbingClient c, GitReadTreeArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
