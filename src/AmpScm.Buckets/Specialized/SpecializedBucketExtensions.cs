using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    public static partial class SpecializedBucketExtensions
    {
        public static Bucket SHA1(this Bucket bucket, Action<byte[]> created)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
            return new CreateHashBucket(bucket, System.Security.Cryptography.SHA1.Create(), created);
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
        }

        public static Bucket SHA256(this Bucket bucket, Action<byte[]> created)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return new CreateHashBucket(bucket, System.Security.Cryptography.SHA256.Create(), created);
        }

        public static Bucket MD5(this Bucket bucket, Action<byte[]> created)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
            return new CreateHashBucket(bucket, System.Security.Cryptography.MD5.Create(), created);
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
        }

        public static Bucket Crc32(this Bucket bucket, Action<int> created)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            return new CreateHashBucket(bucket, CreateHashBucket.Crc32.Create(), (v) => created(NetBitConverter.ToInt32(v, 0)));
        }

        public static Bucket ReadLength(this Bucket bucket, Action<long> bytesRead)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            return new CreateHashBucket(bucket, CreateHashBucket.BytesRead.Create(), (v) => bytesRead(BitConverter.ToInt64(v, 0)));
        }

        public static async ValueTask ReadSkipUntilEofAsync(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            while (0 != await bucket.ReadSkipAsync(int.MaxValue).ConfigureAwait(false))
            { }
        }

        public static Bucket NormalizeEols(this Bucket bucket, BucketEol acceptedEols, BucketEol producedEol = BucketEol.LF)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return new EolNormalizeBucket(bucket, acceptedEols, producedEol);
        }

        public static Bucket NormalizeText(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return new TextNormalizeBucket(bucket);
        }

        public static async ValueTask<BucketBytes> ReadFullAsync(this Bucket bucket, int requested)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            byte[]? resultBuffer = null;
            int collected = 0;
            while (true)
            {
                var bb = await bucket.ReadAsync(requested).ConfigureAwait(false);

                if (collected == 0)
                {
                    if (bb.Length == requested || bb.IsEof)
                        return bb;

                    resultBuffer = bb.ToArray();
                    collected = bb.Length;
                }
                else if (collected == resultBuffer!.Length)
                {
                    if (bb.IsEof)
                        return resultBuffer;

                    var newBuffer = new byte[requested + collected];
                    Array.Copy(resultBuffer, newBuffer, collected);
                    bb.CopyTo(new Memory<byte>(newBuffer, collected, bb.Length));

                    resultBuffer = newBuffer;
                    collected += bb.Length;
                }
                else
                {
                    if (bb.IsEof)
                        return new BucketBytes(resultBuffer, 0, collected);

                    bb.CopyTo(new Memory<byte>(resultBuffer, collected, bb.Length));
                    collected += bb.Length;
                }
                if (requested == bb.Length)
                    return resultBuffer;

                requested -= bb.Length;
            }
        }

        public static async ValueTask SeekAsync(this Bucket bucket, long newPosition)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (newPosition < 0)
                throw new ArgumentNullException(nameof(newPosition));
            else if (bucket is IBucketSeek seekBucket)
            {
                await seekBucket.SeekAsync(newPosition).ConfigureAwait(false);
                return;
            }

            long curPosition = bucket.Position!.Value;

            if (newPosition < curPosition)
            {
                await bucket.ResetAsync().ConfigureAwait(false);
                curPosition = 0;
            }

            while (curPosition < newPosition)
            {
                long skipped = await bucket.ReadSkipAsync(newPosition - curPosition).ConfigureAwait(false);
                if (skipped == 0)
                    throw new InvalidOperationException($"Unexpected seek failure on {bucket.Name} Bucket position {newPosition} from {curPosition}");

                curPosition += skipped;
            }
        }

        public static async ValueTask<int> ReadNetworkInt32Async(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            var bb = await bucket.ReadFullAsync(sizeof(int)).ConfigureAwait(false);

            if (bb.Length != sizeof(uint))
                throw new BucketException($"Unexpected EOF while reading from {bucket.Name} bucket");

            return NetBitConverter.ToInt32(bb, 0);
        }


        public static async ValueTask<byte?> ReadByteAsync(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            var bb = await bucket.ReadAsync(1).ConfigureAwait(false);

            if (bb.Length != 1)
                return null;

            return bb[0];
        }

        [CLSCompliant(false)]
        public static async ValueTask<uint> ReadNetworkUInt32Async(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            var bb = await bucket.ReadFullAsync(sizeof(uint)).ConfigureAwait(false);

            if (bb.Length != sizeof(uint))
                throw new BucketException($"Unexpected EOF while reading from {bucket.Name} bucket");

            return NetBitConverter.ToUInt32(bb, 0);
        }

        public static async ValueTask<long> ReadNetworkInt64Async(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            var bb = await bucket.ReadFullAsync(sizeof(long)).ConfigureAwait(false);

            if (bb.Length != sizeof(ulong))
                throw new BucketException($"Unexpected EOF while reading from {bucket.Name} bucket");

            return NetBitConverter.ToInt64(bb, 0);
        }

        [CLSCompliant(false)]
        public static async ValueTask<ulong> ReadNetworkUInt64Async(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            var bb = await bucket.ReadFullAsync(sizeof(ulong)).ConfigureAwait(false);

            if (bb.Length != sizeof(ulong))
                throw new BucketException($"Unexpected EOF while reading from {bucket.Name} bucket");

            return NetBitConverter.ToUInt64(bb, 0);
        }

        public static T[] ArrayAppend<T>(this T[] array, T item)
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));

            T[] nw = new T[array.Length + 1];
            Array.Copy(array, 0, nw, 0, array.Length);
            nw[array.Length] = item;
            return nw;
        }

        internal static ArraySegment<byte>[] AsArraySegments(this ReadOnlyMemory<byte>[] bytes)
        {
            return null!;
        }
    }
}

