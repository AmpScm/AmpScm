﻿namespace AmpScm.Git.Client.Porcelain;

public enum AllowAlwaysNever
{
    Allow,
    Always,
    Never,
}
public class GitMergeArgs : GitPorcelainArgs
{
    public string? Message { get; set; }
    public bool AppendLog { get; set; }

    public AllowAlwaysNever FastForward { get; set; }
    public bool Sign { get; set; }

    public override void Verify()
    {
    }
}

public partial class GitPorcelain
{
    [GitCommand("merge")]
    public static async ValueTask Merge(this GitPorcelainClient c, string source, GitMergeArgs? options = null)
    {
        if (string.IsNullOrEmpty(source))
            throw new ArgumentNullException(nameof(source));

        await Merge(c, new[] { source }, options).ConfigureAwait(false);
    }

    [GitCommand("merge")]
    public static async ValueTask Merge(this GitPorcelainClient c, string[] source, GitMergeArgs? options = null)
    {
        if (!(source?.Any() ?? false))
            throw new ArgumentOutOfRangeException(nameof(source));

        options?.Verify();
        options ??= new();

        List<string> args = new();

        if (options.AppendLog)
            args.Add("--log");
        if (options.Sign)
            args.Add("-S");

        if (options.FastForward == AllowAlwaysNever.Never)
            args.Add("--no-ff");
        else if (options.FastForward == AllowAlwaysNever.Always)
            args.Add("--ff-only");

        if (!string.IsNullOrEmpty(options.Message))
        {
            args.Add("-m");
            args.Add(options.Message!);
        }

        args.Add("--");
        args.AddRange(source);

        await c.Repository.RunGitCommandAsync("merge", args).ConfigureAwait(false);
    }
}
