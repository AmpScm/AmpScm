using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;
using AmpScm.Git.Objects.Writers;

namespace AmpScm.Git.Objects;

public abstract class GitObjectWriter
{
    public GitId? Id { get; private protected set; }

    public abstract GitObjectType Type { get; }

    public abstract ValueTask<GitId> WriteToAsync(GitRepository repository);

    private protected async ValueTask<GitId> WriteBucketAsObject(Bucket bucket, GitRepository repository, CancellationToken cancellationToken = default)
    {
        string tmpFile = Guid.NewGuid().ToString() + ".tmp";
        var di = Directory.CreateDirectory(Path.Combine(repository.GitDirectory, "objects", "info"));
        var tmpFilePath = Path.Combine(di.FullName, tmpFile);
        string? tmpFile2 = null;
        GitId? id = null;
        string newName;

        using (var f = GitInstallFile.Create(tmpFilePath))
        {
            try
            {
                long? r = await bucket.ReadRemainingBytesAsync().ConfigureAwait(false);
                if (!r.HasValue)
                {
                    string innerTmp = Path.Combine(Path.GetTempPath(), tmpFile) + ".pre";

                    using (var tmp = File.Create(innerTmp))
                    {
                        tmpFile2 = innerTmp;
                        await bucket.WriteToAsync(tmp, cancellationToken).ConfigureAwait(false);
                        r = tmp.Length;
                    }
                    bucket = FileBucket.OpenRead(innerTmp);
                }

                await Type.CreateHeader(r.Value!).Append(bucket)
                        .GitHash(repository.InternalConfig.IdType, cs => id = cs)
                        .Compress(BucketCompressionAlgorithm.ZLib, BucketCompressionLevel.Maximum)
                    .WriteToAsync(f, cancellationToken).ConfigureAwait(!false);

                string idName = id!.ToString();

                var dir = Path.Combine(repository.GitDirectory, "objects", idName.Substring(0, 2));
                Directory.CreateDirectory(dir);

                newName = Path.Combine(dir, idName.Substring(2));
            }
            catch when (!GitInstallFile.TrySetDeleteOnClose(f, deleteFile: true))
            {
                f.Close();

                try
                {
                    File.Delete(tmpFilePath);
                }
                catch (UnauthorizedAccessException)
                { }
                catch (IOException)
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

            if (!File.Exists(newName))
            {
                if (GitInstallFile.TryMoveFile(f, newName))
                    return id;
            }
            else
            {
                if (GitInstallFile.TrySetDeleteOnClose(f, deleteFile: true))
                    return id;
            }
        }

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
