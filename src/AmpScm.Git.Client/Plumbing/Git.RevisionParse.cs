namespace AmpScm.Git.Client.Plumbing;

public class GitRevisionParseArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("rev-parse")]
    public static async ValueTask RevisionParse(this GitPlumbingClient c, GitRevisionParseArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
