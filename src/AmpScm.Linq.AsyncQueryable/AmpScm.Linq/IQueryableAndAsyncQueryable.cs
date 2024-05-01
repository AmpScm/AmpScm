[assembly: CLSCompliant(true)]

namespace AmpScm.Linq;

#pragma warning disable CA1010 // Generic interface should also be implemented
public interface IQueryableAndAsyncQueryable : IQueryable, IAsyncQueryable
#pragma warning restore CA1010 // Generic interface should also be implemented
{
}

public interface IQueryableAndAsyncQueryable<out T> : IQueryable<T>, IAsyncQueryable<T>, IQueryableAndAsyncQueryable
{

}

#pragma warning disable CA1010 // Generic interface should also be implemented
public interface IOrderedQueryableAndAsyncQueryable : IOrderedQueryable, IOrderedAsyncQueryable
#pragma warning restore CA1010 // Generic interface should also be implemented
{

}

public interface IOrderedQueryableAndAsyncQueryable<out T> : IQueryableAndAsyncQueryable<T>, IOrderedQueryable<T>, IOrderedAsyncEnumerable<T>, IOrderedQueryableAndAsyncQueryable
{

}
