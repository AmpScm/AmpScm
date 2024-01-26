using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Git.Implementation;
using AmpScm.Git.Sets;
using AmpScm.Linq;

namespace AmpScm.Git.Sets;

public class GitObjectSet<T> : GitSet<T>, IQueryableAndAsyncQueryable<T>
    where T : GitObject
{
    internal GitObjectSet(GitRepository repository, Expression<Func<GitObjectSet<T>>> rootExpression)
        : base(repository)
    {
        Expression = (rootExpression?.Body as MemberExpression) ?? throw new ArgumentNullException(nameof(rootExpression));
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return Repository.SetQueryProvider.GetAsyncEnumerator<T>(cancellationToken);
    }

    internal bool ContainsId(GitId id)
    {
        return Repository.ObjectRepository.ContainsId(id);
    }

    public override IEnumerator<T> GetEnumerator()
    {
        return Repository.SetQueryProvider.GetEnumerable<T>().GetEnumerator();
    }

    public ValueTask<T?> GetAsync(GitId id)
    {
        return Repository.SetQueryProvider.GetByIdAsync<T>(id);
    }

    public ValueTask<T?> ResolveIdAsync(string idString)
    {
        return Repository.ObjectRepository.ResolveIdString<T>(idString);
    }

#pragma warning disable CA1043 // Use Integral Or String Argument For Indexers
    public T? this[GitId id]
#pragma warning restore CA1043 // Use Integral Or String Argument For Indexers
    {
        get => Repository.SetQueryProvider.GetByIdAsync<T>(id).AsTask().Result;
    }
}
