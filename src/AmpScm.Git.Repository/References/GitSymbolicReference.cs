using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.References
{
    public class GitSymbolicReference : GitReference
    {
        object? _reference;

        internal GitSymbolicReference(GitReferenceRepository repository, string name)
            : base(repository, name, (GitId?)null)
        {
        }

        public override async ValueTask ReadAsync()
        {
            if (_reference is null)
            {
                if (GitRepository.TryReadRefFile(Path.Combine(Repository.GitDir, Name), "ref: ", out var val))
                    _reference = val;
            }
        }

        public GitReference? Reference
        {
            get
            {
                if (_reference is null)
                    ReadAsync().AsTask().GetAwaiter().GetResult();

                if (_reference is string r)
                {
                    _reference = Repository.Repository.ReferenceRepository.GetUnsafeAsync(r, false).AsTask().Result ?? _reference;
                }

                return _reference as GitReference;
            }
        }

        public string? ReferenceName
        {
            get => (_reference as string) ?? Reference?.Name ?? (_reference as string); // Last for later resolved
        }

        public override GitObject? GitObject => Reference?.GitObject;

        public override GitCommit? Commit => Reference?.Commit;

        public override GitId? Id => Reference?.Id;
    }
}
