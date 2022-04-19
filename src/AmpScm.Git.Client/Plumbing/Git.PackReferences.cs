using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public class GitPackReferencesArgs : GitPlumbingArgs
    {
        public bool All { get; set; }
        public bool NoPrune { get; set; }

        public override void Verify()
        {

        }
    }

    partial class GitPlumbing
    {
        [GitCommand("pack-refs")]
        public static async ValueTask PackReferences(this GitPlumbingClient c, GitPackReferencesArgs a)
        {
            a.Verify();

            await c.Repository.RunPlumbingCommand("pack-refs", new [] {
                a.All ? "--all" : "",
                a.NoPrune ? "--no-prune" : ""
            }.Where(x=>!string.IsNullOrEmpty(x)).ToArray());
        }
    }
}
