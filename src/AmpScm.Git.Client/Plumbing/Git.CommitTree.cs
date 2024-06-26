﻿namespace AmpScm.Git.Client.Plumbing;

public class GitCommitTreeArgs : GitPlumbingArgs
{
    public override void Verify()
    {
        throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("commit-tree")]
    public static async ValueTask CommitTree(this GitPlumbingClient c, GitCommitTreeArgs options)
    {
        options.Verify();
        //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

        await c.ThrowNotImplemented();
    }
}
