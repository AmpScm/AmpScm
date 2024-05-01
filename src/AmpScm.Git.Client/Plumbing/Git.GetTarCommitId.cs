namespace AmpScm.Git.Client.Plumbing;

public class GitGetTarCommitIdArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("get-tar-commit-id")]
    public static async ValueTask GetTarCommitId(this GitPlumbingClient c, GitGetTarCommitIdArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
