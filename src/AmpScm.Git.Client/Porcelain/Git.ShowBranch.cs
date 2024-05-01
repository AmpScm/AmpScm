namespace AmpScm.Git.Client.Porcelain;

public class GitShowBranchArgs : GitPorcelainArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPorcelain
{
    [GitCommand("show-branch")]
    public static async ValueTask ShowBranch(this GitPorcelainClient c, GitShowBranchArgs? options = null)
    {
        options?.Verify();
        //var (_, txt) = await c.Repository.RunPorcelainCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
