using System.Collections;
using System.Linq.Expressions;

namespace AmpScm.Linq;

internal sealed class NonAsyncQueryableWrapper<T> : IQueryableAndAsyncQueryable<T>, IOrderedQueryableAndAsyncQueryable<T>
{
    private readonly NonAsyncProviderWrapper _provider;
    private readonly IAsyncQueryable<T> _queryable;

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

    IAsyncQueryProvider IAsyncQueryable.Provider => throw new NotSupportedException();

    public IOrderedAsyncEnumerable<T> CreateOrderedAsyncEnumerable<TKey>(Func<T, TKey> keySelector, IComparer<TKey>? comparer, bool descending)
    {
        return (_queryable as IOrderedQueryableAndAsyncQueryable<T>)?.CreateOrderedAsyncEnumerable(keySelector, comparer, descending)
            ?? throw new NotSupportedException();
    }

    public IOrderedAsyncEnumerable<T> CreateOrderedAsyncEnumerable<TKey>(Func<T, CancellationToken, ValueTask<TKey>> keySelector, IComparer<TKey>? comparer, bool descending)
    {
        return (_queryable as IOrderedQueryableAndAsyncQueryable<T>)?.CreateOrderedAsyncEnumerable(keySelector, comparer, descending)
            ?? throw new NotSupportedException();
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
            while (r.MoveNextAsync().AsTask().Result)
            {
                yield return r.Current;
            }
        }
        finally
        {
            r.DisposeAsync().AsTask().Wait();
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        var r = _queryable.GetAsyncEnumerator();
        try
        {
            while (r.MoveNextAsync().AsTask().Result)
            {
                yield return r.Current;
            }
        }
        finally
        {
            r.DisposeAsync().AsTask().Wait();
        }
    }
}

internal sealed class NonAsyncProviderWrapper : QueryAndAsyncQueryProvider
{
    private IAsyncQueryProvider QueryProvider { get; }

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
        return QueryProvider.ExecuteAsync<object>(expression, CancellationToken.None).AsTask().Result;
    }

    public override TResult Execute<TResult>(Expression expression)
    {
        return QueryProvider.ExecuteAsync<TResult>(expression, CancellationToken.None).AsTask().Result;
    }

    public override ValueTask<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken token)
    {
        return QueryProvider.ExecuteAsync<TResult>(expression, token);
    }
}
