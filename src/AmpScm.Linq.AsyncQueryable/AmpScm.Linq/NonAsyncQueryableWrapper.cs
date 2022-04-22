using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmpScm.Linq
{
    internal sealed class NonAsyncQueryableWrapper<T> : IQueryableAndAsyncQueryable<T>, IOrderedQueryableAndAsyncQueryable<T>
    {
        readonly NonAsyncProviderWrapper _provider;
        readonly IAsyncQueryable<T> _queryable;

        public NonAsyncQueryableWrapper(IAsyncQueryable<T> inner, NonAsyncProviderWrapper wrapper)
        {
            _provider = wrapper ?? throw new ArgumentNullException(nameof(wrapper));
            _queryable = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public NonAsyncQueryableWrapper(IAsyncQueryable<T> inner)
            : this(inner, new NonAsyncProviderWrapper(inner?.Provider ?? throw new ArgumentNullException(nameof(inner))))
        {

        }

        public Type ElementType => typeof(T);

        public Expression Expression => _queryable.Expression;

        public IQueryProvider Provider => _provider;

        IAsyncQueryProvider IAsyncQueryable.Provider => throw new NotImplementedException();

        public IOrderedAsyncEnumerable<T> CreateOrderedEnumerable<TKey>(Func<T, TKey> keySelector, IComparer<TKey>? comparer, bool descending)
        {
            return (_queryable as IOrderedQueryableAndAsyncQueryable<T>)?.CreateOrderedEnumerable(keySelector, comparer, descending)
                ?? throw new NotImplementedException();
        }

        public IOrderedAsyncEnumerable<T> CreateOrderedEnumerable<TKey>(Func<T, ValueTask<TKey>> keySelector, IComparer<TKey>? comparer, bool descending)
        {
            return (_queryable as IOrderedQueryableAndAsyncQueryable<T>)?.CreateOrderedEnumerable(keySelector, comparer, descending)
                ?? throw new NotImplementedException();
        }

        public IOrderedAsyncEnumerable<T> CreateOrderedEnumerable<TKey>(Func<T, CancellationToken, ValueTask<TKey>> keySelector, IComparer<TKey>? comparer, bool descending)
        {
            return (_queryable as IOrderedQueryableAndAsyncQueryable<T>)?.CreateOrderedEnumerable(keySelector, comparer, descending)
                ?? throw new NotImplementedException();
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return _queryable.GetAsyncEnumerator(cancellationToken);
        }

        public IEnumerator<T> GetEnumerator()
        {
            var r = _queryable.GetAsyncEnumerator();
            try
            {
                while (r.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                {
                    yield return r.Current;
                }
            }
            finally
            {
                r.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            var r = _queryable.GetAsyncEnumerator();
            try
            {
                while (r.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                {
                    yield return r.Current;
                }
            }
            finally
            {
                r.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }

    sealed class NonAsyncProviderWrapper : QueryAndAsyncQueryProvider
    {
        IAsyncQueryProvider QueryProvider { get; }

        public NonAsyncProviderWrapper(IAsyncQueryProvider asyncQueryProvider)
        {
            QueryProvider = asyncQueryProvider;
        }

        public override IQueryableAndAsyncQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            var q = QueryProvider.CreateQuery<TElement>(expression);
            var p = q.Provider;

            return new NonAsyncQueryableWrapper<TElement>(q,
                    ReferenceEquals(p, QueryProvider) ? this : new NonAsyncProviderWrapper(p));
        }

        public override object? Execute(Expression expression)
        {
            return QueryProvider.ExecuteAsync<object>(expression, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        public override TResult Execute<TResult>(Expression expression)
        {
            return QueryProvider.ExecuteAsync<TResult>(expression, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        public override ValueTask<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken token)
        {
            return QueryProvider.ExecuteAsync<TResult>(expression, token);
        }
    }
}
