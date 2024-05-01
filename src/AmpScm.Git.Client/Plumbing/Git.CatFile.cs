namespace AmpScm.Git.Client.Plumbing;

public class GitCatFileArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("cat-file")]
    public static async ValueTask CatFile(this GitPlumbingClient c, GitCatFileArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
