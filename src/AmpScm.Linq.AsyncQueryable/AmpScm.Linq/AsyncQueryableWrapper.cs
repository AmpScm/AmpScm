using System.Collections;
using System.Linq.Expressions;

namespace AmpScm.Linq;

internal sealed class AsyncQueryableWrapper<T> : IQueryableAndAsyncQueryable<T>, IOrderedQueryableAndAsyncQueryable<T>
{
    private AsyncQueryableProviderWrapper AsyncProvider { get; }
    private IQueryable<T> InnerQueryable { get; }

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

    IOrderedAsyncEnumerable<T> IOrderedAsyncEnumerable<T>.CreateOrderedAsyncEnumerable<TKey>(Func<T, TKey> keySelector, IComparer<TKey>? comparer, bool descending)
    {
        throw new NotSupportedException();
    }

    IOrderedAsyncEnumerable<T> IOrderedAsyncEnumerable<T>.CreateOrderedAsyncEnumerable<TKey>(Func<T, CancellationToken, ValueTask<TKey>> keySelector, IComparer<TKey>? comparer, bool descending)
    {
        throw new NotSupportedException();
    }
}

internal sealed class AsyncQueryableProviderWrapper : QueryAndAsyncQueryProvider
{
    private IQueryProvider QueryProvider { get; }

    public AsyncQueryableProviderWrapper(IQueryProvider provider)
    {
        QueryProvider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override IQueryableAndAsyncQueryable<TElement> CreateQuery<TElement>(Expression expression)
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
