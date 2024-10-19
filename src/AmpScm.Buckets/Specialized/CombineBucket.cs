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

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        try
        {
            if (disposing)
            {
                await RightSource.DisposeAsync();
            }
        }
        finally
        {
            await base.DisposeAsync(disposing);
        }
    }

}
