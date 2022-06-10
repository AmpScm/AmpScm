using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Porcelain
{
    public class GitMergeArgs : GitPorcelainArgs
    {
        public string? Message { get; set; }
        public bool AppendLog { get; set; }

        public override void Verify()
        {
        }
    }

    partial class GitPorcelain
    {
        [GitCommand("merge")]
        public static async ValueTask Merge(this GitPorcelainClient c, string source, GitMergeArgs? options = null)
        {
            if (string.IsNullOrEmpty(source))
                throw new ArgumentNullException(nameof(source));

            await Merge(c, new[] { source }, options);
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

            if (!string.IsNullOrEmpty(options.Message))
            {
                args.Add("-m");
                args.Add(options.Message);
            }

            args.Add("--");
            args.AddRange(source);

            await c.Repository.RunGitCommandAsync("merge", args);
        }
    }
}
