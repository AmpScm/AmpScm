using System.Threading.Tasks;

namespace AmpScm.Buckets.Git;

public abstract class GitBucket : WrappingBucket
{
    protected GitBucket(Bucket source) : base(source)
    {
    }
}
