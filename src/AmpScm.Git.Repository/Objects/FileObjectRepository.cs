using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Git.Objects
{
    internal sealed class FileObjectRepository : GitObjectRepository
    {
        readonly string _objectsDir;

        public FileObjectRepository(GitRepository repository, string objectsDir)
            : base(repository, "Blobs:" + objectsDir)
        {
            _objectsDir = objectsDir;
        }

        public override async ValueTask<TGitObject?> GetByIdAsync<TGitObject>(GitId id)
            where TGitObject : class
        {
            var name = id.ToString();

            string path = Path.Combine(_objectsDir, name.Substring(0, 2), name.Substring(2));

            if (!File.Exists(path))
                return null;

            var fileReader = FileBucket.OpenRead(path, false);
            try
            {
                var rdr = new GitObjectFileBucket(fileReader);

                GitObject ob = await GitObject.FromBucketAsync(Repository, rdr, id).ConfigureAwait(false);

                if (ob is TGitObject tg)
                    return tg;

                rdr.Dispose();

                return null;
            }
            catch
            {
                fileReader.Dispose();
                throw;
            }
        }

        internal override bool ContainsId(GitId id)
        {
            var name = id.ToString();

            string path = Path.Combine(_objectsDir, name.Substring(0, 2), name.Substring(2));

            return File.Exists(path);
        }

        internal override ValueTask<GitObjectBucket?> ResolveById(GitId id)
        {
            var name = id.ToString();

            string path = Path.Combine(_objectsDir, name.Substring(0, 2), name.Substring(2));

            if (!File.Exists(path))
                return default;

            var fileReader = FileBucket.OpenRead(path, false);
            return new ValueTask<GitObjectBucket?>(new GitObjectFileBucket(fileReader));
        }

        public override async IAsyncEnumerable<TGitObject> GetAll<TGitObject>(HashSet<GitId> alreadyReturned)
        {
            foreach (string dir in Directory.GetDirectories(_objectsDir, "??"))
            {
                string prefix = Path.GetFileName(dir);

                foreach (var file in Directory.GetFiles(dir))
                {
                    string idString = prefix + Path.GetFileName(file);

                    if (!GitId.TryParse(idString, out var id))
                        continue;

                    var fileReader = FileBucket.OpenRead(file, false);

                    var rdr = new GitObjectFileBucket(fileReader);

                    GitObject ob = await GitObject.FromBucketAsync(Repository, rdr, id).ConfigureAwait(false);

                    if (ob is TGitObject tg)
                        yield return tg;
                    else
                        rdr.Dispose();
                }
            }
        }

        internal override async ValueTask<(T? Result, bool Success)> DoResolveIdString<T>(string idString, GitId baseGitId)
            where T : class
        {
#pragma warning disable CA1308 // Normalize strings to uppercase
            string idLow = idString.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

            string pf = idLow.Substring(0, 2);
            string subDir = Path.Combine(_objectsDir, pf);

            if (!Directory.Exists(subDir))
                return (null, true); // No matches

            string[] files = Directory.GetFiles(subDir, idLow.Substring(2) + "*");

            if (files.Length == 0)
                return (null, true); // No matches
            else if (files.Length == 1)
                return (await GetByIdAsync<T>(GitId.Parse(pf + Path.GetFileName(files[0]))).ConfigureAwait(false), true);
            else
                return (null, false); // Multiple matches
        }

        internal override bool ProvidesCommitInfo => false;
    }
}
