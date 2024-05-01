using AmpScm.Buckets.Interfaces;
using AmpScm.Git;

namespace AmpScm.Buckets.Git;

public abstract class GitObjectBucket : GitBucket, IBucketSeek
{
    protected GitObjectBucket(Bucket source) : base(source)
    {
    }

    public abstract ValueTask<GitObjectType> ReadTypeAsync();

    public abstract ValueTask SeekAsync(long newPosition);
}
