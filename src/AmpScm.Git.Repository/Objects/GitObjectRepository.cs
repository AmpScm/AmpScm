using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;
using AmpScm.Git.Objects;
using AmpScm.Git.Repository;

namespace AmpScm.Git.Objects
{
    public abstract class GitObjectRepository : GitBackendRepository
    {
        private protected GitObjectRepository(GitRepository repository, string key)
            : base(repository)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
        }

        internal virtual string Key { get; }

        public virtual IAsyncEnumerable<TGitObject> GetAll<TGitObject>(HashSet<GitId> alreadyReturned)
            where TGitObject : GitObject
        {
            return AsyncEnumerable.Empty<TGitObject>();
        }


        public virtual ValueTask<TGitObject?> GetByIdAsync<TGitObject>(GitId id)
            where TGitObject : GitObject
        {
            return default;
        }

        internal virtual bool ContainsId(GitId id)
        {
            return false;
        }

        internal virtual ValueTask<GitObjectBucket?> ResolveById(GitId id)
        {
            return default;
        }

        internal virtual ValueTask<IGitCommitGraphInfo?> GetCommitInfo(GitId id)
        {
            return default;
        }

        public ValueTask<GitObjectBucket?> FetchGitIdBucketAsync(GitId id)
        {
            return Repository.ObjectRepository.ResolveById(id);
        }

        /// <summary>
        /// Gets a rough estimate on how many objects there are in the repository
        /// </summary>
        /// <remarks>The actual providers may produce more or less details, but the number should be good enough for guessing a usable id length</remarks>
        public virtual long ObjectCount => 0;

        internal async ValueTask<TGitObject?> ResolveIdString<TGitObject>(string idString)
            where TGitObject : GitObject
        {
            if (string.IsNullOrEmpty(idString))
                throw new ArgumentNullException(nameof(idString));
            else if (idString.Length <= 2)
                throw new ArgumentOutOfRangeException(nameof(idString), "Need at least two characters for id resolving");

            string idBase = idString.PadRight(40, '0');

            if (GitId.TryParse(idBase, out var baseGitId))
                return (await DoResolveIdString<TGitObject>(idString, baseGitId).ConfigureAwait(false)).Result;
            else
                return null;
        }

        internal virtual ValueTask<(T? Result, bool Success)> DoResolveIdString<T>(string idString, GitId baseGitId)
            where T : GitObject
        {
            return new ValueTask< (T? Result, bool Success)>((null, true));
        }

        internal virtual bool ProvidesCommitInfo => true;
        internal virtual bool ProvidesGetObject => true;

        internal static GitObjectType? ObjectType<TGitObject>() where TGitObject : GitObject
        {
            if (typeof(TGitObject) == typeof(GitBlob))
                return GitObjectType.Blob;
            else if (typeof(TGitObject) == typeof(GitTree))
                return GitObjectType.Tree;
            else if (typeof(TGitObject) == typeof(GitCommit))
                return GitObjectType.Commit;
            else if (typeof(TGitObject) == typeof(GitTagObject))
                return GitObjectType.Tag;
            else
                return null;
        }

        protected override void Dispose(bool disposing)
        {
        }

        private protected static GitObjectType GetGitObjectType(Type type)
        {
            if (type == typeof(GitCommit))
                return GitObjectType.Commit;
            else if (type == typeof(GitTree))
                return GitObjectType.Tree;
            else if (type == typeof(GitBlob))
                return GitObjectType.Blob;
            else if (type == typeof(GitTagObject))
                return GitObjectType.Tag;
            else
                throw new InvalidOperationException();
        }
    }
}
