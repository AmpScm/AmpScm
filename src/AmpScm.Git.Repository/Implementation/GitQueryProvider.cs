﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Git.Sets;
using AmpScm.Linq.AsyncQueryable;

namespace AmpScm.Git.Implementation
{
    internal class GitQueryProvider : IAsyncQueryProvider, IGitQueryRoot
    {
        public GitQueryProvider(GitRepository repository)
        {
            Repository = repository;
        }

        public GitRepository Repository { get; }

        public IQueryable CreateQuery(Expression expression)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            Type? type = GetElementType(expression.Type);

            if (type == null)
                throw new ArgumentOutOfRangeException(nameof(expression));

            return (IQueryable)Activator.CreateInstance(typeof(GitQuery<>).MakeGenericType(type), new object[] { this, expression! })!;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new GitQuery<TElement>(this, expression);
        }

        public object? Execute(Expression expression)
        {
            expression = new GitQueryVisitor().Visit(expression);

            return Expression.Lambda<Func<object>>(expression).Compile().Invoke();
        }

        public TResult Execute<TResult>(Expression expression)
        {
            expression = new GitQueryVisitor().Visit(expression);

            return Expression.Lambda<Func<TResult>>(expression).Compile().Invoke();
        }

        internal IAsyncEnumerable<T> GetNamedAsyncEnumerable<T>(CancellationToken cancellationToken = default)
        {
            if (typeof(T) == typeof(GitReference))
                return (IAsyncEnumerable<T>)Repository.ReferenceRepository.GetAll();
            else if (typeof(T) == typeof(GitRemote))
                return (IAsyncEnumerable<T>)Repository.Configuration.GetAllRemotes();
            else if (typeof(T) == typeof(GitBranch))
                return (IAsyncEnumerable<T>)GetNamedAsyncEnumerable<GitReference>().Where(x => x.IsBranch).Select(x => new GitBranch(x));
            else if (typeof(T) == typeof(GitTag))
                return (IAsyncEnumerable<T>)GetNamedAsyncEnumerable<GitReference>().Where(x => x.IsTag).Select(x => new GitTag(x));

            return Enumerable.Empty<T>().ToAsyncEnumerable();
        }

        internal IEnumerable<TResult> GetNamedEnumerable<TResult>()
        {
            if (typeof(TResult) == typeof(GitReference))
                return (IEnumerable<TResult>)Repository.ReferenceRepository.GetAll().AsNonAsyncEnumerable();

            return GetNamedAsyncEnumerable<TResult>(CancellationToken.None).AsNonAsyncEnumerable();
        }

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            expression = new GitQueryVisitor().Visit(expression);

            return Expression.Lambda<Func<TResult>>(expression).Compile().Invoke();
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
            return Repository.ObjectRepository.GetAll<TResult>(new HashSet<GitId>()).GetAsyncEnumerator();
        }

        public IEnumerable<TResult> GetEnumerable<TResult>()
            where TResult : GitObject
        {
            return Repository.ObjectRepository.GetAll<TResult>(new HashSet<GitId>()).AsNonAsyncEnumerable();
        }

        public IQueryable<TResult> GetAll<TResult>() where TResult : GitObject
        {
            return GetEnumerable<TResult>().AsQueryable();
        }

        public async ValueTask<TResult?> GetByIdAsync<TResult>(GitId oid) where TResult : GitObject
        {
            return await Repository.ObjectRepository.GetByIdAsync<TResult>(oid).ConfigureAwait(false);
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
            return GetNamedEnumerable<TResult>().AsQueryable();
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
            return WrapEnumerable(set).AsQueryable();
        }

        public IQueryable<GitReferenceChange> GetAllReferenceChanges(GitReferenceChangeSet set)
        {
            return WrapEnumerable(set).AsQueryable();
        }


        static IEnumerable<T> WrapEnumerable<T>(IEnumerable<T> r)
        {
            foreach (var v in r)
                yield return v;
        }
    }
}
