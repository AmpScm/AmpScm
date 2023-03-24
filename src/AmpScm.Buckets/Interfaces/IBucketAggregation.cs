namespace AmpScm.Buckets.Interfaces
{
    internal interface IBucketAggregation
    {
        Bucket Append(Bucket bucket);
        Bucket Prepend(Bucket bucket);
    }
}
