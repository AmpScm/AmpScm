using System.Text;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Git;

public static class GitBucketExtensions
{
    /// <summary>
    /// Return length of this hash in bytes
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static int HashLength(this GitIdType type)
    {
        return GitId.HashLength(type);
    }

    /// <summary>
    /// Returns true if the element represents a file on disk, otherwise false
    /// </summary>
    /// <param name="elementType"></param>
    /// <returns></returns>
    public static bool IsFile(this GitTreeElementType elementType)
    {
        return elementType switch
        {
            GitTreeElementType.File => true,
            GitTreeElementType.FileExecutable => true,
            GitTreeElementType.SymbolicLink => true,
            _ or GitTreeElementType.Directory or GitTreeElementType.GitCommitLink or GitTreeElementType.None => false,
        };
    }

    /// <summary>
    /// Returns true if the element represents a directory on disk, otherwise false
    /// </summary>
    /// <param name="elementType"></param>
    /// <returns></returns>
    public static bool IsDirectory(this GitTreeElementType elementType)
    {
        return elementType switch
        {
            GitTreeElementType.Directory => true,
            GitTreeElementType.GitCommitLink => true,
            _ or GitTreeElementType.File or GitTreeElementType.FileExecutable or GitTreeElementType.SymbolicLink or GitTreeElementType.None => false,
        };
    }

    public static Bucket CreateHeader(this GitObjectType type, long length)
    {
        string txt;
        switch (type)
        {
            case GitObjectType.Blob:
                txt = $"blob {length}\0";
                break;
            case GitObjectType.Tree:
                txt = $"tree {length}\0";
                break;
            case GitObjectType.Commit:
                txt = $"commit {length}\0";
                break;
            case GitObjectType.Tag:
                txt = $"tag {length}\0";
                break;
            default:
            case GitObjectType.None:
                throw new ArgumentOutOfRangeException(nameof(type), type, message: null);
        }

        return Encoding.ASCII.GetBytes(txt).AsBucket();
    }

    public static Bucket GitHash(this Bucket bucket, GitIdType type, Action<GitId> created)
    {
        if (bucket is null)
            throw new ArgumentNullException(nameof(bucket));

        switch (type)
        {
            case GitIdType.Sha1:
                return bucket.SHA1(x => created(new GitId(type, x)));
            case GitIdType.Sha256:
                return bucket.SHA256(x => created(new GitId(type, x)));
            default:
            case GitIdType.None:
                throw new ArgumentOutOfRangeException(nameof(type), type, message: null);
        }
    }
}
