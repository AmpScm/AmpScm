using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Objects
{
    internal sealed class CommitGraphChainRepository : GitObjectRepository
    {
        private string chain;
        List<CommitGraphRepository>? Graphs;

        public CommitGraphChainRepository(GitRepository repository, string chain) : base(repository, "CommitChain:" + chain)
        {
            this.chain = chain;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && Graphs is not null)
                {
                    foreach (var g in Graphs)
                        g.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }

        }

        public override ValueTask<TGitObject?> GetByIdAsync<TGitObject>(GitId id)
            where TGitObject : class
        {
            return default;
        }

        public IEnumerable<CommitGraphRepository> Chains
        {
            get
            {
                if (Graphs != null)
                    return Graphs;

                var list = new List<CommitGraphRepository>();
                try
                {
                    foreach (var line in File.ReadAllLines(Path.Combine(chain, "commit-graph-chain")))
                    {
                        string file = Path.Combine(chain, $"graph-{line.TrimEnd()}.graph");

                        if (File.Exists(file))
                            list.Add(new CommitGraphRepository(Repository, file));
                    }
                }
                catch (IOException)
                { }
                return Graphs = list;
            }
        }

        public override async IAsyncEnumerable<TGitObject> GetAll<TGitObject>(HashSet<GitId> alreadyReturned)
        {
            if (!typeof(TGitObject).IsAssignableFrom(typeof(GitCommit)))
                yield break;

            foreach (var v in Chains)
            {
                await foreach (var ob in v.GetAll<TGitObject>(alreadyReturned))
                {
                    yield return ob;
                }
            }
        }

        internal override async ValueTask<IGitCommitGraphInfo?> GetCommitInfo(GitId id)
        {
            foreach (var v in Chains)
            {
                var info = await v.GetCommitInfo(id).ConfigureAwait(false);

                if (info != null)
                    return info;
            }
            return null;
        }

        internal override bool ProvidesGetObject => false;
    }
}
