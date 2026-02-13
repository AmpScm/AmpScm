namespace AmpScm.Git.Client.Plumbing;

public class GitRepositoryArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("repo")]
    public static async ValueTask Repository(this GitPlumbingClient c, GitRepositoryArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
