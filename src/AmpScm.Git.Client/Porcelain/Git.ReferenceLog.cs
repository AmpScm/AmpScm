namespace AmpScm.Git.Client.Porcelain;

public class GitReferenceLogArgs : GitPorcelainArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPorcelain
{
    [GitCommand("reflog")]
    public static async ValueTask ReferenceLog(this GitPorcelainClient c, GitReferenceLogArgs? options = null)
    {
        options?.Verify();
        //var (_, txt) = await c.Repository.RunPorcelainCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
