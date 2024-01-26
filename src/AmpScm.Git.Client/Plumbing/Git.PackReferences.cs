using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing;

public class GitPackReferencesArgs : GitPlumbingArgs
{
    public bool All { get; set; }
    public bool NoPrune { get; set; }

    public override void Verify()
    {

    }
}

public partial class GitPlumbing
{
    [GitCommand("pack-refs")]
    public static async ValueTask PackReferences(this GitPlumbingClient c, GitPackReferencesArgs options)
    {
        options.Verify();

        await c.Repository.RunGitCommandAsync("pack-refs", new [] {
            options.All ? "--all" : "",
            options.NoPrune ? "--no-prune" : ""
        }.Where(x=>!string.IsNullOrEmpty(x)).ToArray());
    }
}
