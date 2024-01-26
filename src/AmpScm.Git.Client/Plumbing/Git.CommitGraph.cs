using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public enum GitCommitGraphSplit
    {
        SingleFile,
        Split,
        NoMerge,
        Replace
    }


    public class GitCommitGraphArgs : GitPlumbingArgs
    {
        public GitCommitGraphSplit Split { get; set; }
        public bool VerifyCommitGraph { get; set; }
        public override void Verify()
        {
            //throw new NotImplementedException();
        }
    }

    public partial class GitPlumbing
    {
        [GitCommand("commit-graph")]
        public static async ValueTask CommitGraph(this GitPlumbingClient c, GitCommitGraphArgs options)
        {
            var args = new List<string>();

            options?.Verify();
            options ??= new();

            if (options.VerifyCommitGraph)
            {
                args.Add("verify");
            }
            else
            {
                args.Add("write");

                switch(options.Split)
                {
                    case GitCommitGraphSplit.SingleFile:
                        break;
                    case GitCommitGraphSplit.NoMerge:
                        args.Add("--split=no-merge");
                        break;
                    case GitCommitGraphSplit.Replace:
                        args.Add("--split=replace");
                        break;
                    default:
                        args.Add("--split");
                        break;
                }
            }

            await c.Repository.RunGitCommandAsync("commit-graph", args);

            Porcelain.GitPorcelain.RemoveReadOnlyIfNecessary(c.Repository.GitDirectory);
        }
    }
}
