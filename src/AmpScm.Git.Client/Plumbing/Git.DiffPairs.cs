namespace AmpScm.Git.Client.Plumbing;

public class GitDiffPairsArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("diff-pairs")]
    public static async ValueTask DiffPairs(this GitPlumbingClient c, GitDiffPairsArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
