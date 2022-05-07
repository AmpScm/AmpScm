using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public class GitMergeBaseArgs : GitPlumbingArgs
    {
        public override void Verify()
        {
            throw new NotImplementedException();
        }
    }

    partial class GitPlumbing
    {
        [GitCommand("merge-base")]
        public static async ValueTask MergeBase(this GitPlumbingClient c, GitMergeBaseArgs options)
        {
            options.Verify();
            //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

            await c.ThrowNotImplemented();
        }
    }
}
