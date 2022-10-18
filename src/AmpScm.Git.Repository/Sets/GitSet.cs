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
using AmpScm.Buckets.Git;
using AmpScm.Git.Implementation;
using AmpScm.Linq;

namespace AmpScm.Git.Sets
{
    [DebuggerDisplay("{DebuggerDisplay}")]
    public class GitSet
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected GitRepository Repository { get; }

        internal GitSet(GitRepository repository)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }


        private Type GetElementType()
        {
            if (this is IQueryable q)
                return q.ElementType;
            else if (GetType().GetInterfaces().Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>)).Select(x => x.GetGenericArguments()[0]).FirstOrDefault() is Type t)
                return t;
            else
                return typeof(object);
        }

        private protected string MakePluralElementName()
        {
            string name = GetElementType().Name;

            if (name.EndsWith("ch", StringComparison.Ordinal))
                return name + "es";
            else
                return name + "s";
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"Set of {MakePluralElementName()}";
    }

    public abstract class GitSet<T> : GitSet, IEnumerable<T>, IQueryableAndAsyncQueryable, IListSource
        where T : class, IGitObject
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected Expression Expression { get; set; } = default!;
        internal GitSet(GitRepository repository) : base(repository)
        {
        }

        internal GitSet(GitRepository repository, Expression<Func<GitSet<T>>> rootExpression)
            : this(repository)
        {
            Expression = (rootExpression?.Body as MemberExpression) ?? throw new ArgumentNullException(nameof(rootExpression));
        }

#pragma warning disable CA1033 // Interface methods should be callable by child types
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        Type IQueryable.ElementType => typeof(T);
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        Type IAsyncQueryable.ElementType => typeof(T);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IQueryProvider IQueryable.Provider => Repository.SetQueryProvider;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IAsyncQueryProvider IAsyncQueryable.Provider => Repository.SetQueryProvider;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        Expression IQueryable.Expression => Expression;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        Expression IAsyncQueryable.Expression => Expression;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
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
