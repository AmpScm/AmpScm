using System.Diagnostics;
using AmpScm.Git;

namespace AmpScm.Buckets.Git.Objects;

public record GitTreeElementRecord
{
    public GitTreeElementType Type { get; init; }
    public string Name { get; init; } = default!;
    public GitId Id { get; init; } = default!;

    public Bucket AsBucket()
    {
        return
            Bucket.Create.FromUTF8(
                $"{Convert.ToString((int)Type, 8)} {Name}\0")  // "100644 MyFile\0"
            + Id.Hash.AsBucket();                              // Hashcode

    }
}

public sealed class GitTreeObjectBucket : GitBucket
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly GitIdType _idType;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _checkedType;

    public GitTreeObjectBucket(Bucket source, GitIdType idType) : base(source)
    {
        _idType = idType;
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        while (await ReadTreeElementRecord().ConfigureAwait(false) != null)
        {

        }

        return BucketBytes.Eof;
    }

    public async ValueTask<GitTreeElementRecord?> ReadTreeElementRecord()
    {
        if (!_checkedType && Source is GitObjectBucket gobb)
        {
            var type = await gobb.ReadTypeAsync().ConfigureAwait(false);

            if (type != GitObjectType.Tree)
                throw new GitBucketException($"Bucket {gobb.Name} is not Tree but {type}");

            _checkedType = true;
        }

        var (bb, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.Zero, eolState: null).ConfigureAwait(false);

        if (bb.IsEof)
            return null;

        if (eol != BucketEol.Zero)
            throw new GitBucketException("Truncated tree");

        int nSep = bb.IndexOf(' ');
        if (nSep < 0)
            throw new GitBucketException("Truncated tree. No mask separator");

        string name = bb.ToUTF8String(nSep + 1, eol);
        string mask = bb.ToASCIIString(0, nSep);

        var id = await Source.ReadGitIdAsync(_idType).ConfigureAwait(false);

        return new GitTreeElementRecord
        {
            Name = name,
            Type = (GitTreeElementType)Convert.ToInt32(mask, 8),
            Id = id
        };
    }
}
