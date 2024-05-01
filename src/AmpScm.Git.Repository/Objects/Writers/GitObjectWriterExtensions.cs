using AmpScm.Buckets;

namespace AmpScm.Git.Objects;

public static partial class GitObjectWriterExtensions
{
    public static GitTreeWriter AsWriter(this GitTree tree)
    {
        if (tree is null)
            throw new ArgumentNullException(nameof(tree));

        var gtw = GitTreeWriter.CreateEmpty();

        foreach (var v in tree)
        {
            switch (v.ElementType)
            {
                case GitTreeElementType.File:
                case GitTreeElementType.FileExecutable:
                case GitTreeElementType.Directory:
                    gtw.Add(v.Name, v.GitObject.AsLazy());
                    break;
                case GitTreeElementType.SymbolicLink:
                case GitTreeElementType.GitCommitLink:
                    throw new NotSupportedException();
            }
        }
        gtw.PutId(tree.Id); // TODO: Cleanup
        return gtw;
    }

    public static GitBlobWriter AsWriter(this GitBlob blob)
    {
        if (blob is null)
            throw new ArgumentNullException(nameof(blob));

        var bw = GitBlobWriter.CreateFrom(blob.GetBucket());

        bw.PutId(blob.Id); // TODO: Cleanup

        return bw;
    }

    public static GitCommitWriter AsWriter(this GitCommit commit)
    {
        if (commit is null)
            throw new ArgumentNullException(nameof(commit));

        var cw = GitCommitWriter.CreateFromTree(commit.Tree.AsWriter());
        cw.Parents = commit.Parents.Select(x => (x ?? throw new InvalidOperationException()).AsWriter()).ToArray();


        cw.PutId(commit.Id); // TODO: Cleanup

        return cw;
    }

    public static GitTagObjectWriter AsWriter(this GitTagObject tag)
    {
        if (tag is null)
            throw new ArgumentNullException(nameof(tag));

        var tw = GitTagObjectWriter.Create((IGitLazy<GitObject>)tag.GitObject, tag.Name!);

        tw.PutId(tag.Id);
        return tw;
    }

    public static GitObjectWriter AsWriter(this GitObject gitObject)
    {
        switch (gitObject)
        {
            case GitCommit gc:
                return gc.AsWriter();
            case GitBlob gb:
                return gb.AsWriter();
            case GitTree gc:
                return gc.AsWriter();
            case GitTagObject gt:
                return gt.AsWriter();
            default:
                throw new ArgumentOutOfRangeException(nameof(gitObject));
        }
    }

    public static IGitLazy<T> AsLazy<T>(this T gitObject)
        where T : GitObject
    {
        return (IGitLazy<T>)gitObject;
    }

    internal static Type AsType(this GitObjectType type)
    {
        switch (type)
        {
            case GitObjectType.Commit:
                return typeof(GitCommit);
            case GitObjectType.Tree:
                return typeof(GitTree);
            case GitObjectType.Blob:
                return typeof(GitBlob);
            case GitObjectType.Tag:
                return typeof(GitTag);
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
