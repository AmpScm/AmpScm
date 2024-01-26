using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Porcelain;

public class GitCommitArgs : GitPorcelainArgs
{
    public bool All { get; set; }
    public bool Amend { get; set; }
    public string? Message { get; set; }
    public bool Only { get; set; }
    public bool Sign { get; set; }

    public override void Verify()
    {
    }
}

public partial class GitPorcelain
{
    [GitCommand("commit")]
    public static async ValueTask Commit(this GitPorcelainClient c, GitCommitArgs? options)
    {
        await Commit(c, Array.Empty<string>(), options);
    }

    [GitCommand("commit")]
    public static async ValueTask Commit(this GitPorcelainClient c, string[] paths, GitCommitArgs? options)
    {
        options?.Verify();
        options ??= new();

        List<string> args = new();

        if (options.All)
            args.Add("--all");
        if (options.Amend)
            args.Add("--amend");
        if (options.Sign)
            args.Add("-S");

        // TODO: Allow configuring these
        args.Add("--no-edit");
        args.Add("--allow-empty-message");

        if (!string.IsNullOrEmpty(options.Message))
        {
            args.Add("-m");
            args.Add(options.Message!);
        }

        if (paths?.Any() ?? false || (options.Only))
        {
            args.Add("--only");

            if (paths?.Any() ?? false)
            {
                args.Add("--");
                args.AddRange(paths);
            }
        }

        await c.Repository.RunGitCommandAsync("commit", args);
    }
}
