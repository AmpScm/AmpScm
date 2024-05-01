namespace AmpScm.Git.Client.Porcelain;

public class GitMaintenanceArgs : GitPorcelainArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPorcelain
{
    [GitCommand("maintenance")]
    public static async ValueTask Maintenance(this GitPorcelainClient c, GitMaintenanceArgs? options = null)
    {
        options?.Verify();
        //var (_, txt) = await c.Repository.RunPorcelainCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
