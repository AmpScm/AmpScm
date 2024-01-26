using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git.Repository;

namespace AmpScm.Git.References
{
    public abstract class GitReferenceRepository : GitBackendRepository
    {
        public const string Head = "HEAD";

        protected internal string GitDir { get; }
        protected internal string WorkTreeDir { get; }

        protected GitReferenceRepository(GitRepository repository, string gitDir, string workTreeDir)
            : base(repository)
        {
            GitDir = gitDir ?? throw new ArgumentNullException(nameof(gitDir));
            WorkTreeDir = workTreeDir ?? throw new ArgumentNullException(nameof(gitDir));
        }

        protected override void Dispose(bool disposing)
        {
        }

        public abstract IAsyncEnumerable<GitReference> GetAll(HashSet<string> alreadyReturned);

        internal GitReferenceUpdateTransaction CreateUpdateTransaction()
        {
            return new GitReferenceUpdater(this);
        }

        public ValueTask<GitReference?> GetAsync(string name)
        {
            if (!GitReference.ValidName(name, allowSpecialSymbols: true))
                throw new ArgumentOutOfRangeException(nameof(name), name, "Invalid Reference name");

            return GetUnsafeAsync(name);
        }

        protected internal abstract ValueTask<GitReference?> GetUnsafeAsync(string name);

        public virtual IAsyncEnumerable<GitReferenceChange>? GetChanges(GitReference reference)
        {
            return default;
        }

        public virtual ValueTask<IEnumerable<GitReference>> ResolveByOidAsync(GitId id, HashSet<string> processed)
        {
            return default;
        }

        protected internal virtual ValueTask<GitReference?> ResolveAsync(GitReference gitReference)
        {
            return default;
        }

        internal GitReference? GetUnsafe(string v)
        {
            return GetUnsafeAsync(v).AsTask().Result;
        }
    }
}
