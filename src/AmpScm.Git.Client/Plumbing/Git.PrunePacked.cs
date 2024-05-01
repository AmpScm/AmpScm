namespace AmpScm.Git.Client.Plumbing;

public class GitPrunePackedArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("prune-packed")]
    public static async ValueTask PrunePacked(this GitPlumbingClient c, GitPrunePackedArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
