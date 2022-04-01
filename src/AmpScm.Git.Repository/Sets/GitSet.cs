using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Git.Implementation;
using AmpScm.Linq;

namespace AmpScm.Git.Sets
{
    public class GitSet
    {
        protected GitRepository Repository { get; }

        internal GitSet(GitRepository repository)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
    }

    public abstract class GitSet<T> : GitSet, IEnumerable<T>, ISyncAndAsyncQueryable, IListSource
        where T : class, IGitObject
    {
        protected Expression Expression { get; set; } = default!;
        internal GitSet(GitRepository repository) : base(repository)
        {
        }

#pragma warning disable CA1033 // Interface methods should be callable by child types
        Type IQueryable.ElementType => typeof(T);
        Type IAsyncQueryable.ElementType => typeof(T);

        IQueryProvider IQueryable.Provider => Repository.SetQueryProvider;
        IAsyncQueryProvider IAsyncQueryable.Provider => Repository.SetQueryProvider;

        Expression IQueryable.Expression => Expression;
        Expression IAsyncQueryable.Expression => Expression;


        bool IListSource.ContainsListCollection => false;
#pragma warning restore CA1033 // Interface methods should be callable by child types

        public abstract IEnumerator<T> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

#pragma warning disable CA1033 // Interface methods should be callable by child types
        IList IListSource.GetList()
#pragma warning restore CA1033 // Interface methods should be callable by child types
        {
            return this.ToList();
        }
    }
}
