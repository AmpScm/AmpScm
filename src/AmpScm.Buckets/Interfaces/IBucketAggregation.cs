namespace AmpScm.Buckets.Interfaces;

internal interface IBucketAggregation
{
    Bucket Append(Bucket source);
    Bucket Prepend(Bucket source);
}
