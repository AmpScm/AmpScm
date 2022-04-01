using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AmpScm.Linq;

namespace System.Linq
{
    /// <summary>
    /// Like <see cref="Queryable"/>, but then for lists that implement both <see cref="IQueryable{T}"/> and <see cref="IAsyncEnumerable{T}"/>,
    /// which makes both the <see cref="Queryable"/> and <see cref="AsyncEnumerable"/> apis apply, and give the compiler no way to choose.
    /// </summary>
    /// <remarks>Most work is delegated to <see cref="Queryable"/>, as that handles query building in the way we want it</remarks>
    public static partial class AmpAsyncQueryable
    {
        internal static MethodInfo GetMethod<T>(Expression<Action<T>> x)
            => ((MethodCallExpression)x.Body).Method.GetGenericMethodDefinition();

        /// <summary>
        /// Converts a generic <see cref="IEnumerable{T}"/> to a generic <see cref="ISyncAndAsyncQueryable{T}"/>
        /// </summary>
        /// <param name="enumerable"></param>
        /// <returns></returns>
#if NET5_0_OR_GREATER
        [RequiresUnreferencedCode("Enumerating in-memory collections as IQueryable can require unreferenced code because expressions referencing IQueryable extension methods can get rebound to IEnumerable extension methods. The IEnumerable extension methods could be trimmed causing the application to fail at runtime.")]
#endif
        public static ISyncAndAsyncQueryable<T> AsSyncAndAsyncQueryable<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable is null)
                throw new ArgumentNullException(nameof(enumerable));
            if (enumerable is ISyncAndAsyncQueryable<T> r)
                return r;
            else if (enumerable is IQueryable<T> q)
                return AsSyncAndAsyncQueryable(q);

            return AsSyncAndAsyncQueryable(enumerable.AsQueryable());
        }

        /// <summary>
        /// Converts a generic <see cref="IQueryable{T}"/> to a generic <see cref="ISyncAndAsyncQueryable{T}"/>
        /// </summary>
        /// <param name="queryable"></param>
        /// <returns></returns>
        public static ISyncAndAsyncQueryable<T> AsSyncAndAsyncQueryable<T>(this IQueryable<T> queryable)
        {
            if (queryable is ISyncAndAsyncQueryable<T> r)
                return r;
            else
                return new AsyncQueryableWrapper<T>(queryable);
        }

        static MethodInfo _asAsyncQueryable = GetMethod<IQueryable<string>>(x => AmpAsyncQueryable.AsSyncAndAsyncQueryable(x));
        /// <summary>
        /// Wraps an <see cref="IQueryable"/> as an <see cref="ISyncAndAsyncQueryable"/>
        /// </summary>
        /// <param name="queryable"></param>
        /// <returns></returns>
        public static ISyncAndAsyncQueryable AsSyncAndAsyncQueryable(this IQueryable queryable)
        {
            if (queryable is null)
                throw new ArgumentNullException(nameof(queryable));
            else if (queryable is ISyncAndAsyncQueryable r)
                return r;
            else
            {
                return (ISyncAndAsyncQueryable)_asAsyncQueryable.MakeGenericMethod(queryable.ElementType).Invoke(null, new[] { queryable })!;
            }
        }
    }
}
