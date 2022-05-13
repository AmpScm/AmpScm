namespace AmpScm.Buckets.Interfaces
{
    public interface IBucketNoClose
    {
        Bucket NoClose();

        bool HasMoreClosers();
    }
}
