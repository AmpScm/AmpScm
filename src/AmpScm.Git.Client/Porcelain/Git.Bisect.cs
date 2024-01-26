using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Porcelain
{
    public class GitBisectArgs : GitPorcelainArgs
    {
        public override void Verify()
        {
            throw new NotImplementedException();
        }
    }

    public partial class GitPorcelain
    {
        [GitCommand("bisect")]
        public static async ValueTask Bisect(this GitPorcelainClient c, GitBisectArgs? options = null)
        {
            options?.Verify();
            //var (_, txt) = await c.Repository.RunPorcelainCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

            await c.ThrowNotImplemented();
        }
    }
}
