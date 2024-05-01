namespace AmpScm.Git.Client.Porcelain;

public class GitCherryPickArgs : GitPorcelainArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPorcelain
{
    [GitCommand("cherry-pick")]
    public static async ValueTask CherryPick(this GitPorcelainClient c, GitCherryPickArgs? options = null)
    {
        options?.Verify();
        //var (_, txt) = await c.Repository.RunPorcelainCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
