using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets;

public partial class Bucket
{
    /// <summary>
    /// Static bucket that doesn't contain any data
    /// </summary>
    public static Bucket Empty { get; } = new EmptyBucket();


    private protected sealed class EmptyBucket : Bucket, IBucketNoDispose
    {
        public override ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            return EofTask;
        }

        bool IBucketNoDispose.HasMultipleDisposers()
        {
            return false;
        }

        Bucket IBucketNoDispose.NoDispose()
        {
            return this;
        }
    }

    internal async ValueTask<byte?> ReadByteAsync()
    {
        var bb = await ReadAsync(1).ConfigureAwait(false);

        if (bb.Length != 1)
            return null;
        else
            return bb[0];
    }

    public static implicit operator Bucket(Bucket[] buckets)
    {
        return FromBucketArray(buckets);
    }

    public static Bucket FromBucketArray(Bucket[] buckets)
    {
        if (buckets is null || buckets.Length == 0)
            return Empty;
        else if (buckets.Length == 1)
            return buckets[0];

        // HasMultipleDisposers() handles the keepOpen case for us.
        if (buckets[0] is AggregateBucket.SimpleAggregate s && !s.HasMultipleDisposers())
        {
            int n = 1;
            while (n < buckets.Length && buckets[n] is AggregateBucket.SimpleAggregate s2 && !s2.HasMultipleDisposers())
            {
                s.AppendRange(s2.GetBuckets(), 0);
                n++;
            }

            if (n < buckets.Length)
                s.AppendRange(buckets, n);

            return s;
        }
        return new AggregateBucket.SimpleAggregate(buckets);
    }

    /// <summary>
    /// If both <paramref name="first"/> and <paramref name="second"/> are not null, return
    /// a bucket holding both. If either is null, return the other.
    /// </summary>
    /// <param name="first"></param>
    /// <param name="second"></param>
    /// <returns></returns>
    /// <remarks>If <paramref name="first"/> or <paramref name="second"/> is an aggregate bucket,
    /// insert the other in the existing aggregate. Otherwise creates a new aggregate bucket.
    /// Implemented using <see cref="BucketExtensions.Append(Bucket, Bucket)"/></remarks>
#pragma warning disable CA2225 // Operator overloads have named alternates
    public static Bucket operator +(Bucket first, Bucket second)
#pragma warning restore CA2225 // Operator overloads have named alternates
    {
        if (first is null || first is EmptyBucket)
            return second ?? Empty;
        else if (second is null || second is EmptyBucket)
            return first;
        else
            return first.Append(second);
    }
}
