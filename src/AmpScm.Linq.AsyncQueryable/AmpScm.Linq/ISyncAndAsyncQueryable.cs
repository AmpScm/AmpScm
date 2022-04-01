using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: CLSCompliant(true)]

namespace AmpScm.Linq
{
#pragma warning disable CA1010 // Generic interface should also be implemented
    public interface ISyncAndAsyncQueryable : IQueryable, IAsyncQueryable
#pragma warning restore CA1010 // Generic interface should also be implemented
    {
    }

    public interface ISyncAndAsyncQueryable<out T> : IQueryable<T>, IAsyncQueryable<T>, ISyncAndAsyncQueryable
    {

    }

#pragma warning disable CA1010 // Generic interface should also be implemented
    public interface IOrderedSyncAndAsyncQueryable : IOrderedQueryable, IOrderedAsyncQueryable
#pragma warning restore CA1010 // Generic interface should also be implemented
    {

    }

    public interface IOrderedSyncAndAsyncQueryable<out T> : ISyncAndAsyncQueryable<T>, IOrderedQueryable<T>, IOrderedAsyncEnumerable<T>, IOrderedSyncAndAsyncQueryable
    {

    }
}
