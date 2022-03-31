using System;
using System.Collections.Generic;
using AmpScm.Buckets;

namespace AmpScm.BucketTests.Buckets
{
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
                Array.Copy(items, 0, shortItems, 0, n);
                yield return shortItems;
            }
        }
    }
}
