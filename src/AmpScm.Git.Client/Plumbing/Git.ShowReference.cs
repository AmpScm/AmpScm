namespace AmpScm.Git.Client.Plumbing;

public class GitShowReferenceArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("show-ref")]
    public static async ValueTask ShowReference(this GitPlumbingClient c, GitShowReferenceArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
