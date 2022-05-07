using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public class GitIndexPackArgs : GitPlumbingArgs
    {
        public override void Verify()
        {
            throw new NotImplementedException();
        }
    }

    partial class GitPlumbing
    {
        [GitCommand("index-pack")]
        public static async ValueTask IndexPack(this GitPlumbingClient c, GitIndexPackArgs options)
        {
            options.Verify();
            //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

            await c.ThrowNotImplemented();
        }
    }
}
