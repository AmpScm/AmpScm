using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;

namespace AmpScm.Git.References
{
    internal class GitFileReferenceRepository : GitReferenceRepository
    {
        public GitFileReferenceRepository(GitReferenceRepository repository, string gitDir, string workTreeDir)
            : base(repository.Repository, gitDir, workTreeDir)
        {
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async IAsyncEnumerable<GitReference> GetAll(HashSet<string> alreadyReturned)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            string baseDir = Path.GetFullPath(GitDir);

            foreach (string file in Directory.EnumerateFiles(Path.Combine(baseDir, "refs"), "*", SearchOption.AllDirectories))
            {
                if (file.Length > baseDir.Length + 1 && file[baseDir.Length] == Path.DirectorySeparatorChar)
                {
                    string name = file.Substring(baseDir.Length + 1).Replace(Path.DirectorySeparatorChar, '/');

                    yield return new GitReference(this, name, async () => await LoadIdFromFile(file).ConfigureAwait(false));
                }
            }

            foreach (string file in Directory.EnumerateFiles(GitDir))
            {
                string name = Path.GetFileName(file);

                if (GitReference.AllUpper(name) && !alreadyReturned.Contains(name) && !name.EndsWith("MSG", StringComparison.Ordinal))
                    yield return new GitSymbolicReference(this, file.Substring(GitDir.Length + 1));
            }
        }

        public override async ValueTask<IEnumerable<GitReference>> ResolveByOidAsync(GitId id, HashSet<string> processed)
        {
            List<GitReference>? refs = null;

            await foreach (var v in GetAll(new(StringComparer.Ordinal)).ConfigureAwait(false))
            {
                if (v.Id == id)
                {
                    refs ??= new();
                    refs.Add(v);
                }
                else if (v.IsTag)
                {
                    await v.ReadAsync().ConfigureAwait(false);

                    if (v.GitObject is GitTagObject tagObject)
                    {
                        await v.ReadAsync().ConfigureAwait(false);

                        if (v.Commit?.Id == id)
                        {
                            refs ??= new();
                            refs.Add(v);
                        }
                    }
                }
                processed.Add(v.Name);
            }

            return refs ?? Enumerable.Empty<GitReference>();
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected internal override async ValueTask<GitReference?> GetUnsafeAsync(string name)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            bool symbolic = !name.Contains('/', StringComparison.Ordinal);
            string dir = symbolic ? WorkTreeDir : GitDir;
            string fileName = Path.Combine(dir, name);

            if (!File.Exists(fileName))
                return null;

            if (symbolic)
                return new GitSymbolicReference(this, name);
            else
                return new GitReference(this, name, async () => await LoadIdFromFile(fileName).ConfigureAwait(false));
        }

        protected internal override async ValueTask<GitReference?> ResolveAsync(GitReference gitReference)
        {
            string dir = gitReference.Name.Contains('/', StringComparison.Ordinal) ? GitDir : WorkTreeDir;
            string fileName = Path.Combine(dir, gitReference.Name);

            if (!File.Exists(fileName))
                return null;

            if (GitRepository.TryReadRefFile(fileName, prefix: null, out var body))
            {
                if (body.StartsWith("ref: ", StringComparison.OrdinalIgnoreCase))
                {
                    body = body.Substring("ref: ".Length);
                    var ob = await Repository.ReferenceRepository.GetAsync(body.Trim()).ConfigureAwait(false);

                    if (ob is not null)
                        return ob;
                }
                else if (GitId.TryParse(body, out var id))
                    return new GitReference(Repository.ReferenceRepository, gitReference.Name, id);
            }

            return gitReference; // Not symbolic, and exists. Or error and exists
        }

        private async ValueTask<GitId?> LoadIdFromFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException(nameof(fileName));

            if (!GitRepository.TryReadRefFile(fileName, prefix: null, out var body))
                return null;

            if (body.StartsWith("ref:", StringComparison.Ordinal))
            {
                var ob = await Repository.ReferenceRepository.GetAsync(body.Substring(4).Trim()).ConfigureAwait(false);

                return ob?.Id;
            }
            else if (GitId.TryParse(body, out var oid))
                return oid;
            else if (GitId.TryParse(body.Trim(), out oid))
                return oid;
            return null;
        }

        public override IAsyncEnumerable<GitReferenceChange>? GetChanges(GitReference reference)
        {
            string fileName = Path.Combine(GitDir, "logs", reference.Name);

            if (File.Exists(fileName))
                return GetChangesFromRefLogFile(fileName);

            return null;
        }

        private async IAsyncEnumerable<GitReferenceChange>? GetChangesFromRefLogFile(string fileName)
        {
            var fb = FileBucket.OpenRead(fileName);
            using var gr = new GitReferenceLogBucket(fb);

            while (await gr.ReadGitReferenceLogRecordAsync().ConfigureAwait(false) is GitReferenceLogRecord lr)
            {
                yield return new GitReferenceChange(Repository, lr);
            }
        }
    }
}
