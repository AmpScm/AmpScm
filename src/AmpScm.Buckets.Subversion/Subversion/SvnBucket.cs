namespace AmpScm.Buckets.Subversion;

public abstract class SvnBucket : WrappingBucket
{
    protected SvnBucket(Bucket source) : base(source)
    {
    }
}
