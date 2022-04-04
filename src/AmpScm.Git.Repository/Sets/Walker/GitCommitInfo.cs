using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Git.Objects;
using AmpScm.Git.Repository.Implementation;

namespace AmpScm.Git.Sets.Walker
{
    internal class GitCommitInfo : IEquatable<GitCommitInfo>
    {
        object _commit;
        GitCommitGenerationValue _graphValue;
        Lazy<IEnumerable<GitId>> _parents;
        public GitId Id { get; }
        public IEnumerable<GitId> ParentIds => _parents.Value;

        public GitCommitInfo(GitCommit from)
        {
            Id = from.Id;
            _commit = from;
            _parents = new Lazy<IEnumerable<GitId>>(() => from.ParentIds);
        }

        public GitCommitInfo(GitId from, GitRepository repo)
        {
            Id = from;
            _commit = repo;
            _parents = new GitAsyncLazy<IEnumerable<GitId>>(GetParentIds);
        }

        private async ValueTask<IEnumerable<GitId>> GetParentIds()
        {
            if (_commit is GitCommit gc)
                return gc.ParentIds;
            else if (_commit is IGitCommitGraphInfo gi)
                return gi.ParentIds;
            else if (_commit is GitRepository r)
            {
                var info = await r.ObjectRepository.GetCommitInfo(Id).ConfigureAwait(false);

                if (info != null)
                {
                    _parents = new Lazy<IEnumerable<GitId>>(() => info.ParentIds);
                    _commit = info;

                    _graphValue = info.Value;
                    return _parents.Value;
                }

                var c = await r.ObjectRepository.GetByIdAsync<GitCommit>(Id).ConfigureAwait(false);

                if (c != null)
                    _commit = c;
                return c?.ParentIds.ToArray() ?? Enumerable.Empty<GitId>();
            }
            else
                throw new InvalidOperationException();
        }

        public GitCommitGenerationValue ChainInfo
        {
            get
            {
                if (_graphValue.HasValue)
                    return _graphValue;
                else
                {
                    GC.KeepAlive(ParentIds);
                    return _graphValue;
                }
            }
        }

        public override bool Equals(object? obj)
        {
            return (obj is GitCommitInfo other) && Equals(other);
        }

        public bool Equals(GitCommitInfo? other)
        {
            return other?.Id == Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        private async ValueTask<GitCommit?> GetCommit()
        {
            GitCommit? commit = (_commit as GitCommit) ?? await (_commit as GitRepository)!.ObjectRepository.GetByIdAsync<GitCommit>(Id).ConfigureAwait(false) ?? throw new InvalidOperationException();

            _commit = commit;
            return commit;
        }

        internal async Task<long> GetCommitTimeValue()
        {
            return (await GetCommit().ConfigureAwait(false))?.Committer?.When.ToUnixTimeSeconds() ?? 0;
        }

        internal void SetChainInfo(GitCommitGenerationValue newChainInfo)
        {
            _graphValue = newChainInfo;
        }
    }
}
