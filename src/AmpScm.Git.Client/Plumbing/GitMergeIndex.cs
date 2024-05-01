namespace AmpScm.Git.Client.Plumbing;

public class GitMergeIndexArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("merge-index")]
    public static async ValueTask MergeIndex(this GitPlumbingClient c, GitMergeIndexArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
