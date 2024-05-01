namespace AmpScm.Git.Client.Plumbing;

public class GitCheckOutIndexArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("checkout-index")]
    public static async ValueTask CheckOutIndex(this GitPlumbingClient c, GitCheckOutIndexArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
