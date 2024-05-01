namespace AmpScm.Git.Client.Plumbing;

public class GitPackRedundantArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("pack-redundant")]
    public static async ValueTask PackRedundant(this GitPlumbingClient c, GitPackRedundantArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
