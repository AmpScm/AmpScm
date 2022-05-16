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
        /// <summary>
        /// Wraps the bucket with an <see cref="System.Security.Cryptography.SHA1"/> calculator that reports the result on EOF
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="created"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Bucket SHA1(this Bucket bucket, Action<byte[]> created)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
            return new CreateHashBucket(bucket, System.Security.Cryptography.SHA1.Create(), created);
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
        }

        /// <summary>
        /// Wraps the bucket with an <see cref="System.Security.Cryptography.SHA256"/> calculator that reports the result on EOF
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="created"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Bucket SHA256(this Bucket bucket, Action<byte[]> created)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return new CreateHashBucket(bucket, System.Security.Cryptography.SHA256.Create(), created);
        }

        /// <summary>
        /// Wraps the bucket with an <see cref="System.Security.Cryptography.SHA256"/> calculator that reports the result on EOF
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="created"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Bucket SHA512(this Bucket bucket, Action<byte[]> created)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return new CreateHashBucket(bucket, System.Security.Cryptography.SHA512.Create(), created);
        }

        /// <summary>
        /// Wraps the bucket with an <see cref="System.Security.Cryptography.MD5"/> calculator that reports the result on EOF
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="created"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Bucket MD5(this Bucket bucket, Action<byte[]> created)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
            return new CreateHashBucket(bucket, System.Security.Cryptography.MD5.Create(), created);
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
        }

        /// <summary>
        /// Wraps the bucket with a CRC32 calculator that reports the result on EOF
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="created"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Bucket Crc32(this Bucket bucket, Action<int> created)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return new CreateHashBucket(bucket, CreateHashBucket.Crc32.Create(), (v) => created(BitConverter.ToInt32(v, 0)));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="wrapLines"></param>
        /// <param name="addPadding"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Bucket Base64Encode(this Bucket bucket, bool wrapLines = false, bool addPadding = true)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return new Base64EncodeBucket(bucket, wrapLines, addPadding);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bucket"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Bucket Base64Decode(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return new Base64DecodeBucket(bucket);
        }

        /// <summary>
        /// Wraps the bucket with a counter that counts the number of bytes read and reports that on EOF
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="bytesRead"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Bucket ReadLength(this Bucket bucket, Action<long> bytesRead)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            var wp = bucket.WithPosition();
            long initialPosition = wp.Position!.Value;

            return wp.AtEof(() => bytesRead(wp.Position.Value - initialPosition));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static Bucket AtEof(this Bucket bucket, Func<Task> action)
        {
            return new AtEofBucket(bucket, action);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static Bucket AtEof(this Bucket bucket, Action action)
        {
            return new AtEofBucket(bucket, () => { action(); return Task.CompletedTask; });
        }

        /// <summary>
        /// Reads from the bucket until EOF using <see cref="Bucket.ReadSkipAsync(long)"/>
        /// </summary>
        /// <param name="bucket"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static async ValueTask ReadUntilEofAsync(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            while (0 != await bucket.ReadSkipAsync(long.MaxValue).ConfigureAwait(false))
            { }
        }

        /// <summary>
        /// Normalizes the eols specified by <paramref name="acceptedEols"/> in <paramref name="bucket"/> to the format <paramref name="producedEol"/>
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="acceptedEols"></param>
        /// <param name="producedEol"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Bucket NormalizeEols(this Bucket bucket, BucketEol acceptedEols, BucketEol producedEol = BucketEol.LF)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return new EolNormalizeBucket(bucket, acceptedEols, producedEol);
        }

        /// <summary>
        /// Detects the type of bucket by reading some bytes to check for a byte-order-mark and/or by peeking
        /// some data for special character patterns and then convering to UTF-8
        /// </summary>
        /// <param name="bucket"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Bucket NormalizeToUtf8(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return new TextNormalizeBucket(bucket, TextNormalizeBucket.DefaultEncoding);
        }

        /// <summary>
        /// Detects the type of bucket by reading some bytes to check for a byte-order-mark and/or by peeking
        /// some data for special character patterns and then convering to UTF-8
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="fallbackEncoding">The fallback encoding for per-character encodings. If null the
        /// default of <see cref="Encoding.Default"/> is used, unless that is UTF-8, in which
        /// case ISO 88591-1 is used.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Bucket NormalizeToUtf8(this Bucket bucket, Encoding fallbackEncoding)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return new TextNormalizeBucket(bucket, fallbackEncoding ?? TextNormalizeBucket.DefaultEncoding);
        }

        /// <summary>
        /// Converts the bucket from the specified encoding to UTF-8
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static Bucket ConvertToUtf8(this Bucket bucket, Encoding encoding)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (encoding is null)
                throw new ArgumentNullException(nameof(encoding));

            return new TextRecoderBucket(bucket, encoding);
        }

        public static Bucket TextUpdateEncoding(this Bucket bucket, Encoding sourceEncoding, Encoding targetEncoding)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (sourceEncoding is null)
                throw new ArgumentNullException(nameof(sourceEncoding));
            else if (targetEncoding is null)
                throw new ArgumentNullException(nameof(targetEncoding));

            return new TextRecoderBucket(bucket, sourceEncoding, targetEncoding);
        }


        public static async ValueTask<BucketBytes> ReadFullAsync(this Bucket bucket, int requested)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            ByteCollector result = new(requested);
            while (true)
            {
                var bb = await bucket.ReadAsync(requested).ConfigureAwait(false);

                if (result.IsEmpty)
                {
                    if (bb.Length == requested || bb.IsEof)
                        return bb;
                }
                else if (bb.IsEof)
                    return result.AsBytes();
                else if (bb.Length == requested)
                    return result.AsBytes(bb);

                result.Append(bb);
                requested -= bb.Length;
            }
        }

        /// <summary>
        /// Tries to seek <paramref name="bucket"/> to position <paramref name="newPosition"/>, using
        /// operations like <see cref="Bucket.Reset"/> and/or <see cref="Bucket.ReadSkipAsync(long)"/>
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="newPosition"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
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
                bucket.Reset();
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

        /// <summary>
        /// Reads a whole integer using <see cref="NetBitConverter.FromNetwork(int)"/>
        /// </summary>
        /// <param name="bucket"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="BucketException">Unexpected EOF</exception>
        public static async ValueTask<int> ReadNetworkInt32Async(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            var bb = await bucket.ReadFullAsync(sizeof(int)).ConfigureAwait(false);

            if (bb.Length != sizeof(int))
            {
                if (bb.IsEof)
                    throw new BucketEofException(bucket);
                else
                    throw new BucketException($"Bad read of length {bb.Length} from {bucket.Name} Bucket");
            }

            return NetBitConverter.ToInt32(bb, 0);
        }


        /// <summary>
        /// Reads a single byte or <c>null</c> if EOF.
        /// </summary>
        /// <param name="bucket"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="BucketException">Unexpected EOF</exception>
        public static async ValueTask<byte?> ReadByteAsync(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            var bb = await bucket.ReadAsync(1).ConfigureAwait(false);

            if (bb.Length != 1)
            {
                if (bb.IsEof)
                    return null;
                else
                    throw new BucketException($"Bad read of length {bb.Length} from {bucket.Name} Bucket");
            }

            return bb[0];
        }

        /// <summary>
        /// Reads a whole integer using <see cref="NetBitConverter.FromNetwork(uint)"/>
        /// </summary>
        /// <param name="bucket"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="BucketException">Unexpected EOF</exception>
        [CLSCompliant(false)]
        public static async ValueTask<uint> ReadNetworkUInt32Async(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            var bb = await bucket.ReadFullAsync(sizeof(uint)).ConfigureAwait(false);

            if (bb.Length != sizeof(uint))
            {
                if (bb.IsEof)
                    throw new BucketEofException(bucket);
                else
                    throw new BucketException($"Bad read of length {bb.Length} from {bucket.Name} Bucket");
            }

            return NetBitConverter.ToUInt32(bb, 0);
        }

        /// <summary>
        /// Reads a whole long integer using <see cref="NetBitConverter.FromNetwork(long)"/>
        /// </summary>
        /// <param name="bucket"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="BucketException">Unexpected EOF</exception>
        public static async ValueTask<long> ReadNetworkInt64Async(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            var bb = await bucket.ReadFullAsync(sizeof(long)).ConfigureAwait(false);

            if (bb.Length != sizeof(long))
            {
                if (bb.IsEof)
                    throw new BucketEofException(bucket);
                else
                    throw new BucketException($"Bad read of length {bb.Length} from {bucket.Name} Bucket");
            }

            return NetBitConverter.ToInt64(bb, 0);
        }

        /// <summary>
        /// Reads a whole unsigned long integer using <see cref="NetBitConverter.FromNetwork(ulong)"/>
        /// </summary>
        /// <param name="bucket"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="BucketException">Unexpected EOF</exception>
        [CLSCompliant(false)]
        public static async ValueTask<ulong> ReadNetworkUInt64Async(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            var bb = await bucket.ReadFullAsync(sizeof(ulong)).ConfigureAwait(false);

            if (bb.Length != sizeof(ulong))
            {
                if (bb.IsEof)
                    throw new BucketEofException(bucket);
                else
                    throw new BucketException($"Bad read of length {bb.Length} from {bucket.Name} Bucket");
            }

            return NetBitConverter.ToUInt64(bb, 0);
        }

        /// <summary>
        /// Appends a single item to the array <paramref name="array"/>, by making a copy of the array.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static T[] ArrayAppend<T>(this T[] array, T item)
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));

            var nw = new T[array.Length + 1];
            Array.Copy(array, nw, array.Length);
            nw[array.Length] = item;
            return nw;
        }

        /// <summary>
        /// Appends a single byte to the array <paramref name="array"/>, by making a copy of the array.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static byte[] ArrayAppend(this byte[] array, byte item)
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));

            var nw = new byte[array.Length + 1];
            Array.Copy(array, nw, array.Length);
            nw[array.Length] = item;
            return nw;
        }
    }
}

