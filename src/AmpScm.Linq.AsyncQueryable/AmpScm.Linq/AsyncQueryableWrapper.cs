using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Linq;

namespace AmpScm.Linq
{
    internal sealed class AsyncQueryableWrapper<T> : ISyncAndAsyncQueryable<T>, IOrderedSyncAndAsyncQueryable<T>
    {
        AsyncQueryableProviderWrapper AsyncProvider { get; }
        IQueryable<T> InnerQueryable { get; }

        public AsyncQueryableWrapper(IQueryable<T> inner, AsyncQueryableProviderWrapper p)
        {
            InnerQueryable = inner;
            AsyncProvider = p;
        }

        public AsyncQueryableWrapper(IQueryable<T> inner)
            : this(inner, new AsyncQueryableProviderWrapper(inner?.Provider ?? throw new ArgumentNullException(nameof(inner))))
        {
        }

        public Type ElementType => typeof(T);

        public Expression Expression => InnerQueryable.Expression;

        IQueryProvider IQueryable.Provider => AsyncProvider;

        IAsyncQueryProvider IAsyncQueryable.Provider => AsyncProvider;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            foreach (var v in this)
            {
                yield return v;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return InnerQueryable.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        IOrderedAsyncEnumerable<T> IOrderedAsyncEnumerable<T>.CreateOrderedEnumerable<TKey>(Func<T, TKey> keySelector, IComparer<TKey>? comparer, bool descending)
        {
            throw new NotImplementedException();
        }

        IOrderedAsyncEnumerable<T> IOrderedAsyncEnumerable<T>.CreateOrderedEnumerable<TKey>(Func<T, ValueTask<TKey>> keySelector, IComparer<TKey>? comparer, bool descending)
        {
            throw new NotImplementedException();
        }

        IOrderedAsyncEnumerable<T> IOrderedAsyncEnumerable<T>.CreateOrderedEnumerable<TKey>(Func<T, CancellationToken, ValueTask<TKey>> keySelector, IComparer<TKey>? comparer, bool descending)
        {
            throw new NotImplementedException();
        }
    }

    internal sealed class AsyncQueryableProviderWrapper : SyncAndAsyncQueryProvider
    {
        IQueryProvider QueryProvider { get; }

        public AsyncQueryableProviderWrapper(IQueryProvider provider)
        {
            QueryProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public override ISyncAndAsyncQueryable CreateQuery(Expression expression)
        {
            var q = QueryProvider.CreateQuery(expression);
            var el = q.ElementType;

            var m = AmpAsyncQueryable.GetMethod<object>(x => CreateQuery<object>(null!));
            return (ISyncAndAsyncQueryable)m.MakeGenericMethod(el).Invoke(this, new object[] { expression })!;
        }

        public override ISyncAndAsyncQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            var q = QueryProvider.CreateQuery<TElement>(expression);
            var p = q.Provider;

            return new AsyncQueryableWrapper<TElement>(q,
                    ReferenceEquals(p, QueryProvider) ? this : new AsyncQueryableProviderWrapper(p));
        }

        public override object? Execute(Expression expression)
        {
            return QueryProvider.Execute(expression);
        }

        public override TResult Execute<TResult>(Expression expression)
        {
            return QueryProvider.Execute<TResult>(expression);
        }
   
        public override ValueTask<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken token)
        {
            return base.ExecuteAsync<TResult>(expression, token);
        }
    }
}
