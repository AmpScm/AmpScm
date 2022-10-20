using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Porcelain
{
    public enum GitBundleCommand
    {
        Create,
        Verify,
        ListHeads,
        Load,
        Unbundle=Load,
    }
    public class GitBundleArgs : GitPorcelainArgs
    {
        public GitBundleCommand Command { get; set; }
        public int? Version { get; set; }

        public override void Verify()
        {
            //throw new NotImplementedException();
        }
    }

    partial class GitPorcelain
    {
        [GitCommand("bundle")]
        public static async ValueTask Bundle(this GitPorcelainClient c, string bundleFile, GitBundleArgs? options = null)
        {
            options?.Verify();
            options ??= new();

            List<string> args = new();
            switch(options.Command)
            {
                case GitBundleCommand.Create:
                    args.Add("create");
                    args.Add("--quiet");

                    if (options.Version.HasValue)
                        args.Add($"--version={options.Version.Value}");
                    break;
                case GitBundleCommand.Verify:
                    args.Add("verify");
                    args.Add("--quiet");
                    break;
                case GitBundleCommand.ListHeads:
                    args.Add("list-heads");
                    break;
                case GitBundleCommand.Load:
                    args.Add("unbundle");
                    break;
            }
            args.Add(bundleFile.Replace(Path.DirectorySeparatorChar, '/'));


            if (options.Command == GitBundleCommand.Create)
            {
                args.Add("HEAD");
            }

            await c.Repository.RunGitCommandAsync("bundle", args);
        }
    }
}
