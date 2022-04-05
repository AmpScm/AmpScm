using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;

namespace AmpScm.Git.Sets.Walker
{
    internal class GitRevisionWalker : IAsyncEnumerable<GitRevision>
    {
        private GitRevisionSetOptions options;
        Dictionary<GitId, GitCommitInfo> Commits { get; } = new Dictionary<GitId, GitCommitInfo>();
        GitRepository Repository {get;}

        public GitRevisionWalker(GitRevisionSetOptions options, GitRepository repository)
        {
            this.options = options;
            Repository = repository;
        }

        public async IAsyncEnumerator<GitRevision> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            GitCommit? c = null;

            AddCommits(options.Commits);
            c = options.Commits.FirstOrDefault();

            await EnsureInfo().ConfigureAwait(false);

            while (c != null)
            {
                yield return new GitRevision(c);

                c = c.Parent;
            }
        }

        private async ValueTask EnsureInfo()
        {
            Stack<GitCommitInfo> stack = new Stack<GitCommitInfo>();

            foreach(var v in Commits.Values)
            {
                stack.Push(v);
            }

            while(stack.TryPeek(out var c))
            {
                if (c.ChainInfo.HasValue)
                {
                    c = stack.Pop();
                    continue;
                }

                bool gotall = true;
                List<GitCommitInfo>? parents = null;
                foreach(var p in c.ParentIds)
                {
                    var pc = EnsureCommit(p);

                    if (!pc.ChainInfo.HasValue)
                    {
                        gotall = false;
                        stack.Push(pc);
                    }
                    else if (gotall)
                    {
                        parents ??= new List<GitCommitInfo>();
                        parents.Add(pc);
                    }
                }

                if (gotall)
                {
                    c = stack.Pop();

                    if (!c.ChainInfo.HasValue)
                    {
                        int generation;
                        long correctedTimestamp = await c.GetCommitTimeValue().ConfigureAwait(false);
                        if (parents?.Count > 0)
                        {
                            generation = parents.Max(p => p.ChainInfo.Generation) + 1;
                            correctedTimestamp = Math.Max(parents.Max(p => p.ChainInfo.CorrectedTimeValue) + 1, correctedTimestamp);
                        }
                        else
                        {
                            generation = 1;
                        }

                        c.SetChainInfo(new GitCommitGenerationValue(generation, correctedTimestamp));
                    }
                }
            }            
        }

        GitCommitInfo EnsureCommit(GitId id)
        {
            if (!Commits.TryGetValue(id, out var v))
            {
                Commits.Add(id, v = new GitCommitInfo(id, Repository));
            }
            return v;
        }

        private void AddCommits(IEnumerable<GitCommit> commits)
        {
            foreach (var v in commits)
            {
                if (v == null)
                    return;

                if (Commits.ContainsKey(v.Id))
                    return;

                Commits.Add(v.Id, new GitCommitInfo(v.Id, Repository));
            }
        }
    }
}
