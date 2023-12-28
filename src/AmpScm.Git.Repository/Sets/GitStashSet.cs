using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Git.Implementation;
using AmpScm.Linq;

namespace AmpScm.Git.Sets
{
    public class GitStashSet : GitSet<GitStash>, IQueryableAndAsyncQueryable<GitStash>, IListSource, IReadOnlyList<GitStash>
    {
        internal GitStashSet(GitRepository repository, Expression<Func<GitStashSet>> rootExpression) : base(repository)
        {
            Expression = (rootExpression?.Body as MemberExpression) ?? throw new ArgumentNullException(nameof(rootExpression));
        }

        public GitStash this[int index] => StashChanges.Skip(index >= 0 ? index : throw new ArgumentOutOfRangeException(nameof(index)))
            .FirstOrDefault() is { } c ? new GitStash(c) : throw new ArgumentOutOfRangeException(nameof(index));

        IQueryableAndAsyncQueryable<GitReferenceChange> StashChanges => Repository.References["refs/stash"]?.ReferenceChanges ?? AmpAsyncQueryable.Empty<GitReferenceChange>();

        public int Count => StashChanges.Count();

        public async IAsyncEnumerator<GitStash> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            await foreach (var change in StashChanges.ConfigureAwait(false))
            {
                yield return new GitStash(change);
            }
        }

        public override IEnumerator<GitStash> GetEnumerator()
        {
            return this.AsNonAsyncEnumerable().GetEnumerator();
        }
    }
}
