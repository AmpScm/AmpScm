using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using AmpScm.Git.Implementation;
using AmpScm.Git.Sets.Walker;
using AmpScm.Linq;

namespace AmpScm.Git.Sets
{
    public class GitRevisionSet : GitSet<GitRevision>, ISyncAndAsyncQueryable<GitRevision>
    {
        readonly GitRevisionSetOptions _options;

        internal GitRevisionSet(GitRepository repository)
            : this(repository, null)
        {
            _options = new GitRevisionSetOptions();
            Expression = Expression.Call(Expression.Property(Expression.Constant(Repository),
                                GetProperty<GitRepository>(x => x.NoRevisions)),
                                GetMethod(x => x.SetOptions(null!)),
                                Expression.Constant(_options));
        }

        internal GitRevisionSet(GitRepository repository, GitRevisionSetOptions? options)
            : base(repository)
        {
            _options = options ?? new GitRevisionSetOptions();
            Expression = Expression.Call(Expression.Property(Expression.Constant(Repository),
                                GetProperty<GitRepository>(x => x.NoRevisions)),
                                GetMethod(x => x.SetOptions(null!)),
                                Expression.Constant(_options));
        }

        static System.Reflection.PropertyInfo GetProperty<T>(Expression<Func<T, object>> pr)
            => (System.Reflection.PropertyInfo)((MemberExpression)pr.Body).Member;

        static System.Reflection.MethodInfo GetMethod(Expression<Func<GitRevisionSet, object>> pr)
            => ((MethodCallExpression)pr.Body).Method;

        IAsyncEnumerator<GitRevision> IAsyncEnumerable<GitRevision>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken);
        }

        protected virtual async IAsyncEnumerator<GitRevision> GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            var w = new GitRevisionWalker(_options, Repository);

            await foreach (var v in w)
            {
                yield return v;
            }
        }

        public override IEnumerator<GitRevision> GetEnumerator()
        {
            return this.AsNonAsyncEnumerable().GetEnumerator();
        }

        internal GitRevisionSet AddReference(GitReference gitReference)
        {
            if (gitReference?.Commit is GitCommit q)
                return AddCommit(q);

            return this;
        }

        internal GitRevisionSet AddCommit(GitCommit gitCommit)
        {
            return new GitRevisionSet(Repository, _options.AddCommit(gitCommit));
        }

        internal GitRevisionSet SetOptions(GitRevisionSetOptions options)
        {
            return new GitRevisionSet(Repository, options);
        }
    }
}
