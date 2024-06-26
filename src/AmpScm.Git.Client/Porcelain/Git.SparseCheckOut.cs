﻿namespace AmpScm.Git.Client.Porcelain;

public class GitSparseCheckOutArgs : GitPorcelainArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPorcelain
{
    [GitCommand("sparse-checkout")]
    public static async ValueTask SparseCheckOut(this GitPorcelainClient c, GitSparseCheckOutArgs? options = null)
    {
        options?.Verify();
        //var (_, txt) = await c.Repository.RunPorcelainCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
