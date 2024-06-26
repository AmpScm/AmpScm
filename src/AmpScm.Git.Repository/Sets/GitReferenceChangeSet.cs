﻿using System.ComponentModel;
using System.Linq.Expressions;
using AmpScm.Git.Implementation;
using AmpScm.Linq;

namespace AmpScm.Git.Sets;

public class GitReferenceChangeSet : GitSet<GitReferenceChange>, IQueryableAndAsyncQueryable<GitReferenceChange>, IListSource
{
    private readonly GitReference _reference;

    internal GitReferenceChangeSet(GitRepository repository, GitReference reference)
        : base(repository)
    {
        _reference = reference;
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
        Expression = Expression.Property(Expression.Property(Expression.Property(Expression.Constant(Repository), nameof(Repository.References)),
            "Item", Expression.Constant(_reference.Name)), nameof(GitReference.ReferenceChanges));
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
    }

    public IAsyncEnumerator<GitReferenceChange> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return Repository.ReferenceRepository.GetChanges(_reference)?.GetAsyncEnumerator(cancellationToken) ?? AsyncEnumerable.Empty<GitReferenceChange>().GetAsyncEnumerator(cancellationToken);
    }

    public override IEnumerator<GitReferenceChange> GetEnumerator()
    {
        return this.AsNonAsyncEnumerable().GetEnumerator();
    }
}
