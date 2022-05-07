using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public class GitUpdateIndexArgs : GitPlumbingArgs
    {
        public bool SplitIndex { get; set; }

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

            if (options.SplitIndex)
                args.Add("--split-index");


            await c.Repository.RunPlumbingCommandOut("update-index", args.ToArray());
        }
    }
}
