using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Git.Implementation;
using AmpScm.Linq;

namespace AmpScm.Git.Sets
{
    public class GitReferenceChangeSet : GitSet<GitReferenceChange>, ISyncAndAsyncQueryable<GitReferenceChange>, IListSource
    {
        readonly GitReference _reference;
        internal GitReferenceChangeSet(GitRepository repository, GitReference reference) 
            : base(repository)
        {
            _reference = reference;
            Expression = Expression.Property(Expression.Property(Expression.Property(Expression.Constant(Repository), nameof(Repository.References)),
                "Item", Expression.Constant(_reference.Name)), nameof(GitReference.ReferenceChanges));
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
}
