﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.References
{
    internal class GitPackedRefsReferenceRepository : GitReferenceRepository
    {
        public const string PackedRefsFile = "packed-refs";
        public GitPackedRefsReferenceRepository(GitReferenceRepository repository, string gitDir, string workTreeDir)
            : base(repository.Repository, gitDir, workTreeDir)
        {
        }

        [DebuggerDisplay($"{nameof(GitRefPeel.Id)} {nameof(GitRefPeel.Name)} {nameof(GitRefPeel.Peeled)}")]
        private protected sealed class GitRefPeel
        {
            public string Name { get; set; } = null!;
            public GitId Id { get; set; } = null!;
            public GitId? Peeled { get; set; }
        }

        Dictionary<string, GitRefPeel>? _peelRefs;

        async ValueTask Read()
        {
            if (_peelRefs != null)
                return;

            _peelRefs = new Dictionary<string, GitRefPeel>();

            await ReadRefs().ConfigureAwait(false);
        }

        private protected virtual async ValueTask ReadRefs()
        {
            string fileName = Path.Combine(GitDir, PackedRefsFile);

            if (!File.Exists(fileName))
                return;

            try
            {
                using var sr = File.OpenText(fileName);

                var idLength = GitId.HashLength(Repository.InternalConfig.IdType) * 2;

                GitRefPeel? last = null;
                while (await sr.ReadLineAsync().ConfigureAwait(false) is string line)
                {
                    ParseLineToPeel(line, ref last, idLength);
                }
            }
            catch (FileNotFoundException)
            {
                return;
            }
        }

        private protected void ParseLineToPeel(string line, ref GitRefPeel? last, int idLength)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            if (char.IsLetterOrDigit(line, 0) && line.Length > idLength + 1)
            {
                if (GitId.TryParse(line.Substring(0, idLength), out var oid))
                {
                    string name = line.Substring(idLength + 1).Trim();

                    if (GitReference.ValidName(name, false))
                        _peelRefs![name] = last = new GitRefPeel { Name = name, Id = oid };
                }
            }
            else if (line[0] == '^')
            {
                if (last != null && GitId.TryParse(line.Substring(1).TrimEnd(), out var oid))
                {
                    last.Peeled = oid;
                }
                last = null;
            }
        }

        public override async IAsyncEnumerable<GitReference> GetAll()
        {
            await Read().ConfigureAwait(false);

            foreach (var v in _peelRefs!.Values)
            {
                yield return new GitReference(this, v.Name, v.Id).SetPeeled(v.Peeled);
            }
        }

        protected internal override async ValueTask<GitReference?> GetUnsafeAsync(string name, bool findSymbolic)
        {
            await Read().ConfigureAwait(false);

            if (_peelRefs!.TryGetValue(name, out var v))
                return new GitReference(this, v.Name, v.Id).SetPeeled(v.Peeled);

            return null;
        }
    }
}
