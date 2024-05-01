namespace AmpScm.Git.Client.Plumbing;

public class GitVerifyPackArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("verify-pack")]
    public static async ValueTask VerifyPack(this GitPlumbingClient c, GitVerifyPackArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
