using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Sets
{
    internal class GitTagSet : GitNamedSet<GitTag>
    {
        internal GitTagSet(GitRepository repository, Expression<Func<GitNamedSet<GitTag>>> rootExpression) : base(repository, rootExpression)
        {
        }
    }
}
