using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Porcelain
{
    public abstract class GitPorcelainArgs
    {
        public abstract void Verify();
    }

    public static partial class GitPorcelain
    {
        public static GitPorcelainClient GetPorcelain(this GitRepository repository)
        {
            return new GitPorcelainClient(repository);
        }
    }
}
