using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.References
{
    public sealed class GitSymbolicReference : GitReference
    {
        private object? _reference;

        internal GitSymbolicReference(GitReferenceRepository repository, string name)
            : base(repository, name)
        {
        }

        public override async ValueTask ReadAsync()
        {
            if (_reference is null)
            {
                if (GitRepository.TryReadRefFile(Path.Combine(ReferenceRepository.GitDir, Name), "ref: ", out var val))
                {
                    _reference = val;
                    return;
                }

                _reference = await ReferenceRepository.ResolveAsync(this).ConfigureAwait(false);
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
                    _reference = ReferenceRepository.Repository.ReferenceRepository.GetUnsafeAsync(r).AsTask().Result ?? _reference;
                }

                return _reference as GitReference;
            }
        }

        public string? ReferenceName
        {
            get => (_reference as string) ?? Reference?.Name ?? (_reference as string); // Last for later resolved
        }

        public override GitObject? GitObject => ReferenceEquals(Reference, this) ? base.GitObject: Reference?.GitObject;

        public override GitCommit? Commit => ReferenceEquals(Reference, this) ? base.Commit : Reference?.Commit;

        public override GitId? Id => Reference?.Id ?? base.Id;

        public override string ToString()
        {
            return $"{Name}: {ReferenceName}";
        }
    }
}
