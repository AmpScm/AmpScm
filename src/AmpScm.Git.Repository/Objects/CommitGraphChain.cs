using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AmpScm.Git.Objects
{
    internal sealed class CommitGraphChain : GitObjectRepository
    {
        private readonly string _chainDir;
        private List<GitCommitGraph>? Graphs;

        public CommitGraphChain(GitRepository repository, string chain) : base(repository, "CommitChain:" + chain)
        {
            _chainDir = chain;
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

        public IEnumerable<GitCommitGraph> Chains
        {
            get
            {
                if (Graphs != null)
                    return Graphs;

                var list = new List<GitCommitGraph>();
                try
                {
                    foreach (var line in File.ReadAllLines(Path.Combine(_chainDir, "commit-graph-chain")))
                    {
                        string file = Path.Combine(_chainDir, $"graph-{line.TrimEnd()}.graph");

                        if (File.Exists(file) && GitId.TryParse(line.TrimEnd(), out var id))
                            list.Add(new GitCommitGraph(Repository, file, this, id));
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
                await foreach (var ob in v.GetAll<TGitObject>(alreadyReturned).ConfigureAwait(false))
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

        internal GitCommitGraph GetCommitGraph(GitId parentId)
        {
            foreach (var v in Graphs!)
            {
                if (v.GraphId == parentId)
                    return v;
            }

            string file = Path.Combine(_chainDir, $"graph-{parentId}.graph");
            if (File.Exists(file))
            {
                parentId = new GitId(parentId.Type, parentId.Hash.ToArray()); // Create proper long-term id
                var graph = new GitCommitGraph(Repository, file, this, parentId);
                Graphs.Add(graph);

                return graph;
            }

            throw new FileNotFoundException($"Can't find {file}, referenced from other commit-graph");
        }
    }
}
