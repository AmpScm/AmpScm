namespace AmpScm.Git.Client.Plumbing;

public class GitListTreeArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("ls-tree")]
    public static async ValueTask ListTree(this GitPlumbingClient c, GitListTreeArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
