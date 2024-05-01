namespace AmpScm.Git.Client.Plumbing;

public class GitNameRevisionArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("name-rev")]
    public static async ValueTask NameRevision(this GitPlumbingClient c, GitNameRevisionArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
