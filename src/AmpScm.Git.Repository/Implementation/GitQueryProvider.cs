using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Git.Sets;
using AmpScm.Linq;

namespace AmpScm.Git.Implementation;

internal class GitQueryProvider : QueryAndAsyncQueryProvider, IGitQueryRoot
{
    public GitQueryProvider(GitRepository repository)
    {
        Repository = repository;
    }

    public GitRepository Repository { get; }

    public override IQueryableAndAsyncQueryable CreateQuery(Expression expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));

        Type? type = GetElementType(expression.Type);

        if (type == null)
            throw new ArgumentOutOfRangeException(nameof(expression));

        return (IQueryableAndAsyncQueryable)Activator.CreateInstance(typeof(GitQuery<>).MakeGenericType(type), new object[] { this, expression! })!;
    }

    public override IQueryableAndAsyncQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new GitQuery<TElement>(this, expression);
    }

    public override object? Execute(Expression expression)
    {
        expression = new GitQueryVisitor().Visit(expression);

        return Expression.Lambda<Func<object>>(expression).Compile().Invoke();
    }

    public override TResult Execute<TResult>(Expression expression)
    {
        expression = new GitQueryVisitor().Visit(expression);

        return Expression.Lambda<Func<TResult>>(expression).Compile().Invoke();
    }

    internal IAsyncEnumerable<T> GetNamedAsyncEnumerable<T>(CancellationToken cancellationToken = default)
    {
        if (typeof(T) == typeof(GitReference))
            return (IAsyncEnumerable<T>)Repository.ReferenceRepository.GetAll(new(StringComparer.Ordinal));
        else if (typeof(T) == typeof(GitRemote))
            return (IAsyncEnumerable<T>)Repository.Configuration.GetAllRemotes();
        else if (typeof(T) == typeof(GitBranch))
            return (IAsyncEnumerable<T>)GetNamedAsyncEnumerable<GitReference>(cancellationToken).Where(x => x.IsBranch).Select(x => new GitBranch(x));
        else if (typeof(T) == typeof(GitTag))
            return (IAsyncEnumerable<T>)GetNamedAsyncEnumerable<GitReference>(cancellationToken).Where(x => x.IsTag).Select(x => new GitTag(x));

        return Enumerable.Empty<T>().ToAsyncEnumerable();
    }

    internal IEnumerable<TResult> GetNamedEnumerable<TResult>()
    {
        return GetNamedAsyncEnumerable<TResult>(CancellationToken.None).AsNonAsyncEnumerable();
    }

    internal IList GetNamedList<T>()
    {
        return new List<T>(GetNamedEnumerable<T>());
    }

    public List<T> GetList<T>()
        where T : GitObject
    {
        return new List<T>(GetEnumerable<T>());
    }

    public IAsyncEnumerator<TResult> GetAsyncEnumerator<TResult>(CancellationToken cancellationToken = default)
        where TResult : GitObject
    {
        return Repository.ObjectRepository.GetAll<TResult>(new HashSet<GitId>()).GetAsyncEnumerator(cancellationToken);
    }

    public IEnumerable<TResult> GetEnumerable<TResult>()
        where TResult : GitObject
    {
        return Repository.ObjectRepository.GetAll<TResult>(new HashSet<GitId>()).AsNonAsyncEnumerable();
    }

    public IQueryable<TResult> GetAll<TResult>() where TResult : GitObject
    {
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
        return GetEnumerable<TResult>().AsQueryable();
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
    }

    public async ValueTask<TResult?> GetByIdAsync<TResult>(GitId id) where TResult : GitObject
    {
        return await Repository.ObjectRepository.GetByIdAsync<TResult>(id).ConfigureAwait(false);
    }

    internal static Type? GetElementType(Type type)
    {
        if (type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>)) is Type enumerableType)
        {
            return enumerableType.GetGenericArguments()[0];
        }
        else
            return null;
    }

    public IQueryable<TResult> GetAllNamed<TResult>()
        where TResult : class, IGitNamedObject
    {
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
        return GetNamedEnumerable<TResult>().AsQueryable();
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
    }

    public async ValueTask<TResult?> GetNamedAsync<TResult>(string name)
        where TResult : class, IGitNamedObject
    {
        if (typeof(TResult) == typeof(GitReference))
            return await Repository.ReferenceRepository.GetAsync(name).ConfigureAwait(false) as TResult;
        else if (typeof(TResult) == typeof(GitRemote))
            return await Repository.Configuration.GetRemoteAsync(name).ConfigureAwait(false) as TResult;

        return default;
    }

    public IQueryable<GitRevision> GetRevisions(GitRevisionSet set)
    {
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
        return WrapEnumerable(set).AsQueryable();
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
    }

    public IQueryable<GitReferenceChange> GetAllReferenceChanges(GitReferenceChangeSet set)
    {
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
        return WrapEnumerable(set).AsQueryable();
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
    }

    public IQueryable<GitStash> GetAllStashes(GitStashSet set)
    {
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
        return WrapEnumerable(set).AsQueryable();
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
    }

    private static IEnumerable<T> WrapEnumerable<T>(IEnumerable<T> r)
    {
        foreach (var v in r)
            yield return v;
    }
}
