﻿using System.Text;

namespace AmpScm.Buckets.Specialized;

public class BucketFactory
{
    private BucketFactory()
    { }

    internal static BucketFactory Instance { get; } = new();


#pragma warning disable CA1822 // Mark members as static
    public Bucket FromASCII(string value)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));
        else if (value.Length == 0)
            return Bucket.Empty;

        return Encoding.ASCII.GetBytes(value).AsBucket();
    }

    public Bucket FromUTF8(string value)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));
        else if (value.Length == 0)
            return Bucket.Empty;

        return Encoding.UTF8.GetBytes(value).AsBucket();
    }

    public Bucket From(IAsyncEnumerable<BucketBytes> byteEnumerable)
    {
        if (byteEnumerable is null)
            throw new ArgumentNullException(nameof(byteEnumerable));

        return new FromEnumerableBucket(byteEnumerable);
    }
}
