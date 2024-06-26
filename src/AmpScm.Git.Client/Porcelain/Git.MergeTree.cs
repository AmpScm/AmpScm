﻿namespace AmpScm.Git.Client.Porcelain;

public class GitMergeTreeArgs : GitPorcelainArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPorcelain
{
    [GitCommand("merge-tree")]
    public static async ValueTask MergeTree(this GitPorcelainClient c, GitMergeTreeArgs? options = null)
    {
        options?.Verify();
        //var (_, txt) = await c.Repository.RunPorcelainCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
