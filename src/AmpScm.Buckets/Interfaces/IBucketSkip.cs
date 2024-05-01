namespace AmpScm.Buckets.Interfaces;

internal interface IBucketSkip
{
    Bucket Skip(long skipBytes, bool ensure);
}
