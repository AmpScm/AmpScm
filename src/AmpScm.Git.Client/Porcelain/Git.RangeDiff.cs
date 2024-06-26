﻿namespace AmpScm.Git.Client.Porcelain;

public class GitRangeDiffArgs : GitPorcelainArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPorcelain
{
    [GitCommand("range-diff")]
    public static async ValueTask RangeDiff(this GitPorcelainClient c, GitRangeDiffArgs? options = null)
    {
        options?.Verify();
        //var (_, txt) = await c.Repository.RunPorcelainCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
