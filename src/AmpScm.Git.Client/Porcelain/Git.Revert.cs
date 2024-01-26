using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Porcelain
{
    public class GitRevertArgs : GitPorcelainArgs
    {
        public override void Verify()
        {
            throw new NotImplementedException();
        }
    }

    public partial class GitPorcelain
    {
        [GitCommand("revert")]
        public static async ValueTask Revert(this GitPorcelainClient c, GitRevertArgs? options = null)
        {
            options?.Verify();
            //var (_, txt) = await c.Repository.RunPorcelainCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

            await c.ThrowNotImplemented();
        }
    }
}
