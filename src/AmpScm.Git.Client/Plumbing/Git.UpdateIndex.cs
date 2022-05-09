using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public class GitUpdateIndexArgs : GitPlumbingArgs
    {
        public bool? SplitIndex { get; set; }
        public int? IndexVersion { get; set; }
        public bool Refresh { get; set; }
        public bool ReallyRefresh { get; set; }
        public bool Again { get; set; }

        public bool? UntrackedCache { get; set; }

        public override void Verify()
        {
            //throw new NotImplementedException();
        }
    }

    partial class GitPlumbing
    {
        [GitCommand("update-index")]
        public static async ValueTask UpdateIndex(this GitPlumbingClient c, GitUpdateIndexArgs? options=null)
        {
            options ??= new();
            options.Verify();

            List<string> args = new();

            if (options.SplitIndex == true)
                args.Add("--split-index");
            else if (options.SplitIndex == false)
                args.Add("--no-split-index");

            if (options.UntrackedCache == true)
                args.Add("--untracked-cache");
            else if (options.UntrackedCache == false)
                args.Add("--no-untracked-cache");

            if (options.Refresh)
                args.Add("--refresh");
            if (options.ReallyRefresh)
                args.Add("--really-refresh");
            if (options.Again)
                args.Add("--again");
            if (options.IndexVersion.HasValue)
                args.AddRange(new[] { "--index-version", options.IndexVersion.ToString()! });

            args.Add("--");

            await c.Repository.RunPlumbingCommand("update-index", args.ToArray());
        }
    }
}
