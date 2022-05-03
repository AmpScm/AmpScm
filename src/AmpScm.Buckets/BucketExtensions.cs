using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;
using AmpScm.Buckets.Interfaces;
using System.ComponentModel;
using System.Threading;

namespace AmpScm.Buckets
{
    public static partial class BucketExtensions
    {
        public static Bucket Append(this Bucket bucket, Bucket newLast)
        {
            if (bucket is IBucketAggregation col)
                return col.Append(newLast);
            else if (newLast is IBucketAggregation nl)
                return nl.Prepend(bucket);
            else
            {
                return new AggregateBucket(bucket, newLast);
            }
        }

        public static Bucket Prepend(this Bucket bucket, Bucket newFirst)
        {
            if (bucket is IBucketAggregation col)
                return col.Prepend(newFirst);
            else if (newFirst is IBucketAggregation nf)
                return nf.Append(bucket);
            else
            {
                return new AggregateBucket(newFirst, bucket);
            }
        }

        public static Bucket WithPosition(this Bucket bucket, bool alwaysWrap = false)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            if (!alwaysWrap && bucket.Position != null)
                return bucket;

            return new PositionBucket(bucket);
        }

        public static Bucket Take(this Bucket bucket, long limit, bool alwaysWrap = false)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            if (!alwaysWrap && bucket is IBucketTake take)
                return take.Take(limit);
            else
                return new TakeBucket(bucket, limit);
        }

        public static Bucket Skip(this Bucket bucket, long firstPosition, bool alwaysWrap = false)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (firstPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(firstPosition));

            if (!alwaysWrap && bucket is IBucketSkip sb)
                return sb.Skip(firstPosition);
            else
                return new SkipBucket(bucket, firstPosition);
        }

        public static Bucket NoClose(this Bucket bucket, bool alwaysWrap = false)
        {
            if (!alwaysWrap && bucket is IBucketNoClose nc)
                return nc.NoClose();
            else
                return new NoCloseBucket(bucket);
        }

        public static Bucket SeekOnReset(this Bucket bucket)
        {
            return SkipBucket.SeekOnReset(bucket);
        }

        public static Bucket Wrap(this Bucket bucket)
        {
            return new ProxyBucket.Sealed(bucket);
        }

        public static Bucket VerifyBehavior<TBucket>(this TBucket bucket)
            where TBucket : Bucket
        {
            return new VerifyBucket<TBucket>(bucket);
        }

        public static Bucket AsBucket(this byte[] bytes)
        {
            if ((bytes?.Length ?? 0) == 0)
                return Bucket.Empty;

            return new MemoryBucket(bytes!);
        }

        public static Bucket AsBucket(this byte[] bytes, bool copy)
        {
            if ((bytes?.Length ?? 0) == 0)
                return Bucket.Empty;

            if (copy)
            {
                var data = new byte[bytes!.Length];
                Array.Copy(bytes, data, bytes.Length);
                bytes = data;
            }

            return new MemoryBucket(bytes!);
        }

        public static Bucket AsBucket(ReadOnlySpan<byte> bytes)
        {
            return new MemoryBucket(bytes.ToArray());
        }

        public static TlsBucket WithTlsClientFor<TBucket>(this TBucket bucket, string targetHost)
            where TBucket : Bucket, IBucketWriter
        {
            return new TlsBucket(bucket, bucket, targetHost);
        }

        [CLSCompliant(false)]
        public static Bucket AsBucket(this byte[][] bytes)
        {
            return bytes.Select(x => x.AsBucket()).AsBucket();
        }

        [CLSCompliant(false)]
        public static Bucket AsBucket(this byte[][] bytes, bool keepOpen)
        {
            return bytes.Select(x => x.AsBucket()).AsBucket(keepOpen);
        }

        public static Bucket AsBucket(this ReadOnlyMemory<byte> memory)
        {
            return new MemoryBucket(memory);
        }

        public static Bucket AsBucket(this IEnumerable<Bucket> buckets)
        {
            if (!buckets.Any())
                return Bucket.Empty;

            return new AggregateBucket(buckets.ToArray());
        }

        public static Bucket AsBucket(this IEnumerable<Bucket> buckets, bool keepOpen)
        {
            if (!buckets.Any())
                return Bucket.Empty;

            return new AggregateBucket(keepOpen, buckets.ToArray());
        }

        public static Bucket Decompress(this Bucket bucket, BucketCompressionAlgorithm algorithm)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            switch (algorithm)
            {
                case BucketCompressionAlgorithm.ZLib:
                case BucketCompressionAlgorithm.Deflate:
                case BucketCompressionAlgorithm.GZip:
                    return new ZLibBucket(bucket, algorithm, CompressionMode.Decompress);
                case BucketCompressionAlgorithm.Brotli:
#if !NETFRAMEWORK
                    // Available starting with .Net Core
                    return new CompressionBucket(bucket, (inner) => new BrotliStream(inner, CompressionMode.Decompress));
#endif
                // Maybe: ZStd via https://www.nuget.org/packages/ZstdSharp.Port
                default:
                    throw new ArgumentOutOfRangeException(nameof(algorithm));
            }
        }

        public static Bucket Compress(this Bucket bucket, BucketCompressionAlgorithm algorithm)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            switch (algorithm)
            {
                case BucketCompressionAlgorithm.ZLib:
                case BucketCompressionAlgorithm.Deflate:
                    return new ZLibBucket(bucket, algorithm, CompressionMode.Compress);
                case BucketCompressionAlgorithm.GZip:
                    // Could be optimized like zlib, but currently unneeded
                    return new CompressionBucket(bucket, (inner) => new GZipStream(inner, CompressionMode.Compress));
                case BucketCompressionAlgorithm.Brotli:
#if !NETFRAMEWORK
                    // Available starting with .Net Core
                    return new CompressionBucket(bucket, (inner) => new BrotliStream(inner, CompressionMode.Compress));
#endif
                // Maybe: ZStd via https://www.nuget.org/packages/ZstdSharp.Port
                default:
                    throw new ArgumentOutOfRangeException(nameof(algorithm));
            }
        }

        public static async ValueTask<byte[]> ToArrayAsync(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            using (MemoryStream ms = new MemoryStream())
            {
                await ms.WriteAsync(bucket).ConfigureAwait(false);

                return ms.ToArray();
            }
        }

        public static byte[] ToArray(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return ToArrayAsync(bucket).AsTask().Result;
        }

        internal static byte[] AppendBytes(this byte[] array, BucketBytes bytes)
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));

            byte[] bt = new byte[array.Length + bytes.Length];
            array.CopyTo(bt, 0);

            bytes.CopyTo(bt, array.Length);
            return bt;
        }

        internal static byte[] AppendBytes(this IEnumerable<byte> enumerable, BucketBytes bytes)
        {
            if (enumerable is null)
                throw new ArgumentNullException(nameof(enumerable));

            byte[] bt;
            if (enumerable is byte[] arr)
                return AppendBytes(arr, bytes);
            else if (enumerable is ICollection<byte> c)
            {
                bt = new byte[c.Count + bytes.Length];
                c.CopyTo(bt, 0);
                bytes.CopyTo(bt, c.Count);
                return bt;
            }
            else if (enumerable is System.Collections.ICollection cc)
            {
                bt = new byte[cc.Count + bytes.Length];
                cc.CopyTo(bt, 0);
                bytes.CopyTo(bt, cc.Count);
                return bt;
            }
            else if (enumerable is IReadOnlyCollection<byte> ro)
            {
                bt = new byte[ro.Count + bytes.Length];
                int i = 0;
                foreach(var b in ro)
                {
                    bt[i++] = b;
                }
                bytes.CopyTo(bt, ro.Count);
                return bt;
            }
            else
            {
                return enumerable.Concat(bytes.ToArray()).ToArray();
            }
        }

        public static Stream AsStream(this Bucket bucket)
        {
            return new Wrappers.BucketStream(bucket);
        }

        /// <summary>
        /// Wraps <paramref name="bucket"/> as writable stream, writing to <paramref name="writer"/>
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="writer"></param>
        /// <returns></returns>
        public static Stream AsStream(this Bucket bucket, IBucketWriter writer)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return new Wrappers.BucketStream.WithWriter(bucket, writer);
        }

        public static Bucket AsBucket(this Stream stream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            return new Wrappers.StreamBucket(stream);
        }

        public static TextReader AsReader(this Bucket bucket)
        {
            return new StreamReader(bucket.AsStream());
        }

#if NETFRAMEWORK
        internal static string GetString(this System.Text.Encoding encoding, ReadOnlySpan<byte> bytes)
        {
            return encoding.GetString(bytes.ToArray());
        }
#endif

        public static int CharCount(this BucketEol eol)
        {
            return eol switch
            {
                BucketEol.CRLF => 2,
                BucketEol.None => 0,
                _ => 1,
            };
        }

        public static bool All<T>(this ReadOnlySpan<T> span, Func<T, bool> predicate)
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            foreach(var i in span)
            {
                if (!predicate(i))
                    return false;
            }
            return true;
        }

        public static bool Any<T>(this ReadOnlySpan<T> span, Func<T, bool> predicate)
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            foreach (var i in span)
            {
                if (predicate(i))
                    return true;
            }
            return false;
        }
    }
}
