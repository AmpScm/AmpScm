using AmpScm.Buckets;
using BucketTests.Buckets;

namespace AmpScm.BucketTests.Buckets;

public static class BucketTestExtensions
{
    public static Bucket PerByte(this Bucket self)
    {
        return new PerByteBucket(self);
    }

    public static IEnumerable<T[]> SelectPer<T>(this IEnumerable<T> self, int count)
    {
        T[]? items = null;

        int n = 0;
        foreach(var i in self)
        {
            items ??= new T[count];
            items[n++] = i;

            if (n == count)
            {
                yield return items;
                items = null;
                n = 0;
            }
        }

        if (items != null)
        {
            T[] shortItems = new T[n];
            Array.Copy(items, shortItems, n);
            yield return shortItems;
        }
    }

    public static Bucket NoRemaining(this Bucket self) 
    {
        return new NoRemainingBucket(self);
    }

    public static Bucket NoPosition(this Bucket self)
    {
        return new NoPositionBucket(self);
    }
}
