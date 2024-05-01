namespace AmpScm.Git.Client.Plumbing;

public class GitDiffFilesArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("diff-files")]
    public static async ValueTask DiffFiles(this GitPlumbingClient c, GitDiffFilesArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
