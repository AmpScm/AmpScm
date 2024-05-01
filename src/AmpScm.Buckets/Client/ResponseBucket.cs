namespace AmpScm.Buckets.Client;

public abstract class ResponseBucket : WrappingBucket
{
    protected ResponseBucket(Bucket source, BucketWebRequest request)
        : base(source, noDispose: true)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }

    public BucketWebRequest Request { get; }


    public virtual ValueTask ReadHeaders()
    {
        return default;
    }

    public virtual WebHeaderDictionary Headers => throw new NotSupportedException();
    public virtual bool SupportsHeaders => false;

    public virtual string? ContentType => null;

    public virtual long ContentLength => throw new NotSupportedException();
}
