using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Sets
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class GitRemotesSet : GitNamedSet<GitRemote>
    {
        internal GitRemotesSet(GitRepository repository, Expression<Func<GitNamedSet<GitRemote>>> rootExpression)
            : base(repository, rootExpression)
        {
        }


        private string DebuggerDisplay => $"{Enumerable.Count(this)} Remotes";
    }
}
