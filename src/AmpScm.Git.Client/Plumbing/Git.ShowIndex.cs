namespace AmpScm.Git.Client.Plumbing;

public class GitShowIndexArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("show-index")]
    public static async ValueTask ShowIndex(this GitPlumbingClient c, GitShowIndexArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
