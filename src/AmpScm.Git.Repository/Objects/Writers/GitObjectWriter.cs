using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;
using AmpScm.Git.Objects;

namespace AmpScm.Git.Objects
{
    public abstract class GitObjectWriter
    {
        public GitId? Id { get; private protected set; }

        public abstract GitObjectType Type { get; }

        public abstract ValueTask<GitId> WriteToAsync(GitRepository repository);

        private protected async ValueTask<GitId> WriteBucketAsObject(Bucket bucket, GitRepository repository, CancellationToken cancellationToken = default)
        {
            string tmpFile = Guid.NewGuid().ToString() + ".tmp";
            var di = Directory.CreateDirectory(Path.Combine(repository.GitDir, "objects", "info"));
            var tmpFilePath = Path.Combine(di.FullName, tmpFile);
            string? tmpFile2 = null;
            GitId? id = null;
            {
                using var f = File.Create(tmpFilePath);
                try
                {
                    long? r = await bucket.ReadRemainingBytesAsync().ConfigureAwait(false);
                    if (!r.HasValue)
                    {
                        string innerTmp = Path.Combine(di.FullName, tmpFile) + ".pre";

                        using (var tmp = File.Create(innerTmp))
                        {
                            tmpFile2 = innerTmp; 
                            await bucket.WriteToAsync(tmp, cancellationToken).ConfigureAwait(false);
                            r = tmp.Length;
                        }
                        bucket = FileBucket.OpenRead(innerTmp);                        
                    }

                    using (var wb = Type.CreateHeader(r.Value!).Append(bucket)
                        .GitHash(repository.InternalConfig.IdType, cs => id = cs)
                        .Compress(BucketCompressionAlgorithm.ZLib, BucketCompressionLevel.Maximum))
                    {
                        await wb.WriteToAsync(f, cancellationToken).ConfigureAwait(false);
                    }
                }                
                catch
                {
                    f.Close();

                    try
                    {
                        File.Delete(tmpFilePath);                        
                    }
                    catch (UnauthorizedAccessException)
                    { }
                    catch(IOException)
                    { }

                    throw;
                }
                finally
                {
                    try
                    {
                        if (tmpFile2 is not null)
                            File.Delete(tmpFile2);
                    }
                    catch (UnauthorizedAccessException)
                    { }
                    catch (IOException)
                    { }
                }
            }

            string idName = id!.ToString();

            var dir = Path.Combine(repository.GitDir, "objects", idName.Substring(0, 2));
            Directory.CreateDirectory(dir);

            string newName = Path.Combine(dir, idName.Substring(2));
            if (File.Exists(newName))
                File.Delete(tmpFilePath);
            else
                File.Move(tmpFilePath, newName);
            return id;
        }
    }

    public abstract class GitObjectWriter<TGitObject> : GitObjectWriter, IGitLazy<TGitObject>
        where TGitObject : GitObject
    {
        private protected GitObjectWriter()
        {

        }

        internal void PutId(GitId id)
        {
            Id ??= id;
        }
    }
}
