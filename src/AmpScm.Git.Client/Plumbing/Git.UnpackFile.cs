namespace AmpScm.Git.Client.Plumbing;

public class GitUnpackFileArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("unpack-file")]
    public static async ValueTask UnpackFile(this GitPlumbingClient c, GitUnpackFileArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
