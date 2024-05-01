namespace AmpScm.Git.Client.Plumbing;

public class GitPackObjectsArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("pack-objects")]
    public static async ValueTask PackObjects(this GitPlumbingClient c, GitPackObjectsArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
