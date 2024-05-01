namespace AmpScm.Buckets.Interfaces;

internal interface IBucketTake
{
    Bucket Take(long limit, bool ensure = true);
}
