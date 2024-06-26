﻿namespace AmpScm.Git.Client.Porcelain;

public class GitCheckOutArgs : GitPorcelainArgs
{
    public bool Detach { get; set; }

    public override void Verify()
    {
        //throw new NotImplementedException();
    }
}

public partial class GitPorcelain
{
    [GitCommand("checkout")]
    public static ValueTask CheckOut(this GitPorcelainClient c, string branchOrCommit, GitCheckOutArgs? options = null)
    {
        return CheckOut(c, branchOrCommit, targets: null, options);
    }

    [GitCommand("checkout")]
    public static async ValueTask CheckOut(this GitPorcelainClient c, string branchOrCommit, string[]? targets, GitCheckOutArgs? options = null)
    {
        options ??= new();
        options.Verify();

        List<string> args = new();

        if (options.Detach)
            args.Add("--detach");

        if (!string.IsNullOrEmpty(branchOrCommit))
            args.Add(branchOrCommit);

        if (targets?.Any() ?? false)
        {
            args.Add("--");
            args.AddRange(targets);
        }
        else if (string.IsNullOrEmpty(branchOrCommit))
            throw new ArgumentNullException(nameof(branchOrCommit));

        await c.Repository.RunGitCommandAsync("checkout", args).ConfigureAwait(false);
    }
}
