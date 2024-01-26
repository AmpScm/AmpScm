using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Porcelain
{
    public class GitReplaceArgs : GitPorcelainArgs
    {
        public override void Verify()
        {
            throw new NotImplementedException();
        }
    }

    public partial class GitPorcelain
    {
        [GitCommand("replace")]
        public static async ValueTask Replace(this GitPorcelainClient c, GitReplaceArgs? options = null)
        {
            options?.Verify();
            //var (_, txt) = await c.Repository.RunPorcelainCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

            await c.ThrowNotImplemented();
        }
    }
}
