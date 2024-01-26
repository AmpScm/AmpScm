using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Porcelain
{
    public class GitPruneArgs : GitPorcelainArgs
    {
        public override void Verify()
        {
            //throw new NotImplementedException();
        }
    }

    public partial class GitPorcelain
    {
        [GitCommand("prune")]
        public static async ValueTask Prune(this GitPorcelainClient c, GitPruneArgs? options = null)
        {
            options?.Verify();
            options ??= new();

            List<string> args = new List<string>();


            await c.Repository.RunGitCommandAsync("prune", args);
        }
    }
}
