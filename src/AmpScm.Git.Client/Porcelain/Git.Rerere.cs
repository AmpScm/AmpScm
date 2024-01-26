using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Porcelain
{
    public class GitRerereArgs : GitPorcelainArgs
    {
        public override void Verify()
        {
            throw new NotImplementedException();
        }
    }

    public partial class GitPorcelain
    {
        /// <summary>
        /// Reuse recorded resolution (aka Rerere)
        /// </summary>
        /// <param name="c"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        [GitCommand("rerere")]
        public static async ValueTask Rerere(this GitPorcelainClient c, GitRerereArgs? options = null)
        {
            options?.Verify();
            //var (_, txt) = await c.Repository.RunPorcelainCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

            await c.ThrowNotImplemented();
        }
    }
}
