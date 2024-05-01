namespace AmpScm.Buckets.Specialized;

public abstract class CombineBucket : WrappingBucket
{
    protected Bucket LeftSource => Source;
    protected Bucket RightSource { get; }

    protected CombineBucket(Bucket leftSource, Bucket rightSource)
        : base(leftSource)
    {
        RightSource = rightSource ?? throw new ArgumentNullException(nameof(rightSource));
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                RightSource.Dispose();
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

}
