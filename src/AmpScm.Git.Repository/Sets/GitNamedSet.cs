using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Git.Implementation;
using AmpScm.Linq;

namespace AmpScm.Git.Sets
{
    public class GitNamedSet<T> : GitSet<T>, IQueryableAndAsyncQueryable<T>
        where T : class, IGitNamedObject
    {
        internal GitNamedSet(GitRepository repository, Expression<Func<GitNamedSet<T>>> rootExpression)
            : base(repository)
        {
            Expression = (rootExpression?.Body as MemberExpression) ?? throw new ArgumentNullException(nameof(rootExpression));
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return Repository.SetQueryProvider.GetNamedAsyncEnumerable<T>(cancellationToken).GetAsyncEnumerator(cancellationToken);
        }

        public override IEnumerator<T> GetEnumerator()
        {
            return Repository.SetQueryProvider.GetNamedEnumerable<T>().GetEnumerator();
        }

        public ValueTask<T?> GetAsync(string name)
        {
            return Repository.SetQueryProvider.GetNamedAsync<T>(name);
        }

        public T? this[string name]
        {
            get => Repository.SetQueryProvider.GetNamedAsync<T>(name).AsTask().Result;
        }
    }
}
