namespace AmpScm.Git.Client.Plumbing;

public class GitMakeTagArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("mktag")]
    public static async ValueTask MakeTag(this GitPlumbingClient c, GitMakeTagArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
