namespace AmpScm.Buckets.Interfaces
{
    public interface IBucketNoDispose
    {
        Bucket NoDispose();

        bool HasMultipleDisposers();
    }
}
