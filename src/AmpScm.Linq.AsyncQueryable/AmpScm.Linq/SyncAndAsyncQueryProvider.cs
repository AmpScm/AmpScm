using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmpScm.Linq
{
    public abstract class SyncAndAsyncQueryProvider : IQueryProvider, IAsyncQueryProvider, ISyncAndAsyncQueryProvider
    {
        protected SyncAndAsyncQueryProvider()
        {

        }

        public abstract ISyncAndAsyncQueryable CreateQuery(Expression expression);

        IQueryable IQueryProvider.CreateQuery(Expression expression) => CreateQuery(expression);

        public abstract ISyncAndAsyncQueryable<TElement> CreateQuery<TElement>(Expression expression);

        IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression) => CreateQuery<TElement>(expression);
        IAsyncQueryable<TElement> IAsyncQueryProvider.CreateQuery<TElement>(Expression expression) => CreateQuery<TElement>(expression);

        public abstract object? Execute(Expression expression);

        public abstract TResult Execute<TResult>(Expression expression);

        public virtual ValueTask<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken token)
        {
            return new ValueTask<TResult>(Execute<TResult>(expression));
        }
    }
}
