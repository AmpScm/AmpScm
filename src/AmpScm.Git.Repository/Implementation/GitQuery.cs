using System.Collections;
using System.Linq.Expressions;
using AmpScm.Git.Sets;
using AmpScm.Linq;

namespace AmpScm.Git.Implementation;

internal class GitQuery<T> : IOrderedQueryableAndAsyncQueryable<T>
{
    public GitQuery(GitQueryProvider provider, Expression expression)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    public Type ElementType => typeof(T);

    public Expression Expression { get; }

    public GitQueryProvider Provider { get; }

    IQueryProvider IQueryable.Provider => Provider;

    IAsyncQueryProvider IAsyncQueryable.Provider => Provider;

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        foreach (var v in this)
        {
            if (v is IGitObject r)
                await r.ReadAsync().ConfigureAwait(false);

            yield return v;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return Provider.Execute<IEnumerable<T>>(Expression).GetEnumerator();
    }

    IOrderedAsyncEnumerable<T> IOrderedAsyncEnumerable<T>.CreateOrderedEnumerable<TKey>(Func<T, TKey> keySelector, IComparer<TKey>? comparer, bool descending)
    {
        throw new NotSupportedException();
    }

    IOrderedAsyncEnumerable<T> IOrderedAsyncEnumerable<T>.CreateOrderedEnumerable<TKey>(Func<T, ValueTask<TKey>> keySelector, IComparer<TKey>? comparer, bool descending)
    {
        throw new NotSupportedException();
    }

    IOrderedAsyncEnumerable<T> IOrderedAsyncEnumerable<T>.CreateOrderedEnumerable<TKey>(Func<T, CancellationToken, ValueTask<TKey>> keySelector, IComparer<TKey>? comparer, bool descending)
    {
        throw new NotSupportedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
