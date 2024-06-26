﻿namespace AmpScm.Git.Client.Porcelain;

public class GitFormatPatchArgs : GitPorcelainArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPorcelain
{
    [GitCommand("format-patch")]
    public static async ValueTask FormatPatch(this GitPorcelainClient c, GitFormatPatchArgs? options = null)
    {
        options?.Verify();
        //var (_, txt) = await c.Repository.RunPorcelainCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
