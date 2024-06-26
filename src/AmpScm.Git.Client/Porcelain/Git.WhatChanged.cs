﻿namespace AmpScm.Git.Client.Porcelain;

public class GitWhatChangedArgs : GitPorcelainArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPorcelain
{
    [GitCommand("whatchanged")]
    public static async ValueTask WhatChanged(this GitPorcelainClient c, GitWhatChangedArgs? options = null)
    {
        options?.Verify();
        //var (_, txt) = await c.Repository.RunPorcelainCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
