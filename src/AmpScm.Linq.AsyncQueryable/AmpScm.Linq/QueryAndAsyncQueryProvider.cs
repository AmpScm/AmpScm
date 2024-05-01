using System.Linq.Expressions;
using System.Reflection;

namespace AmpScm.Linq;

public abstract class QueryAndAsyncQueryProvider : IQueryProvider, IAsyncQueryProvider, IQueryAndAsyncQueryProvider
{
    protected QueryAndAsyncQueryProvider()
    {

    }

    private static MethodInfo? _createQuery;

    public virtual IQueryableAndAsyncQueryable CreateQuery(Expression expression)
    {
        if (expression is null)
            throw new ArgumentNullException(nameof(expression));

        var tp = expression.Type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && string.Equals(x.Name, nameof(IEnumerable<int>), StringComparison.Ordinal) && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (tp is not null)
        {
            // 99.9% case
            _createQuery ??= typeof(QueryAndAsyncQueryProvider).GetMethods().First(x => string.Equals(x.Name, nameof(CreateQuery), StringComparison.Ordinal) && x.IsGenericMethod);

            return (IQueryableAndAsyncQueryable)_createQuery.MakeGenericMethod(tp.GetGenericArguments()[0]).Invoke(this, new[] { expression })!;
        }
        else
        {
            throw new InvalidOperationException("Expression doesn't return enumerable");
        }
    }

    IQueryable IQueryProvider.CreateQuery(Expression expression) => CreateQuery(expression);

    public abstract IQueryableAndAsyncQueryable<TElement> CreateQuery<TElement>(Expression expression);

    IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression) => CreateQuery<TElement>(expression);
    IAsyncQueryable<TElement> IAsyncQueryProvider.CreateQuery<TElement>(Expression expression) => CreateQuery<TElement>(expression);

    public abstract object? Execute(Expression expression);

    public abstract TResult Execute<TResult>(Expression expression);

    public virtual ValueTask<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken token)
    {
        return new(Execute<TResult>(expression));
    }
}
