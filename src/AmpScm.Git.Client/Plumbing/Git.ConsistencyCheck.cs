using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public class GitConsistencyCheckArgs : GitPlumbingArgs
    {
        public bool Full { get; set; }
        public override void Verify()
        {
            //throw new NotImplementedException();
        }
    }

    partial class GitPlumbing
    {
        [GitCommand("fsck")]
        public static async ValueTask<string> ConsistencyCheck(this GitPlumbingClient c, GitConsistencyCheckArgs? options = null)
        {
            options ??= new();
            options.Verify();
            var args = new List<string>();

            if (options.Full)
                args.Add("--full");

            var (_, txt) = await c.Repository.RunPlumbingCommandOut("fsck", args.ToArray());
            return txt.Replace("\r","", StringComparison.Ordinal).TrimEnd();
        }
    }
}
