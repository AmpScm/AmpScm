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
        /// <summary>
        /// Returns a new bucket, containing the result of both buckets. (May be optimized to re-use the original buckets)
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="newLast"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Returns a new bucket, containing the result of both buckets. (May be optimized to re-use the original buckets)
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="newFirst"></param>
        /// <returns></returns>
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

        /// <summary>
        /// If bucket doesn't provide <see cref="Bucket.Position"/> or <paramref name="alwaysWrap"/> is true, wraps
        /// the bucket with a position calculating bucket
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="alwaysWrap"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Bucket WithPosition(this Bucket bucket, bool alwaysWrap = false)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            if (!alwaysWrap && bucket.Position != null)
                return bucket;

            return new PositionBucket(bucket);
        }

        /// <summary>
        /// Takes exactly <paramref name="length"/> bytes from <paramref name="bucket"/>, providing
        /// position and remaining bytes
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="length"></param>
        /// <param name="alwaysWrap"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <remarks>See also <see cref="Take"/>, which handles truncated streams</remarks>
        public static Bucket TakeExact(this Bucket bucket, long length, bool alwaysWrap = false)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (!alwaysWrap && bucket is IBucketTake take)
                return take.Take(length, true);
            else
                return new TakeBucket(bucket, length, true);
        }

        /// <summary>
        /// Takes at most <paramref name="limit"/> bytes from <paramref name="bucket"/>,
        /// while also adding position support if not provided by the inner <paramref name="bucket"/>.
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="limit"></param>
        /// <param name="alwaysWrap"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <remarks>See also <see cref="TakeExact"/>, which takes an exact amount
        /// of bytes</remarks>
        public static Bucket Take(this Bucket bucket, long limit, bool alwaysWrap = false)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            if (!alwaysWrap && bucket is IBucketTake take)
                return take.Take(limit, false);
            else
                return new TakeBucket(bucket, limit, false);
        }

        /// <summary>
        /// Skips the first <paramref name="skipBytes"/> buckets from bucket
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="skipBytes"></param>
        /// <param name="alwaysWrap"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Bucket SkipExact(this Bucket bucket, long skipBytes, bool alwaysWrap = false)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (skipBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(skipBytes));

            if (!alwaysWrap && bucket is IBucketSkip sb)
                return sb.Skip(skipBytes, true);
            else
                return new SkipBucket(bucket, skipBytes, true);
        }

        /// <summary>
        /// Skips the first <paramref name="skipBytes"/> buckets from bucket
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="skipBytes"></param>
        /// <param name="alwaysWrap"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Bucket Skip(this Bucket bucket, long skipBytes, bool alwaysWrap = false)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (skipBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(skipBytes));

            if (!alwaysWrap && bucket is IBucketSkip sb)
                return sb.Skip(skipBytes, false);
            else
                return new SkipBucket(bucket, skipBytes, false);
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
            if (bucket is IBucketSeekOnReset sr)
                return sr.SeekOnReset();
            else
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

        public static Bucket AsBucket(this IEnumerable<byte> bytes)
        {
            if (bytes is null || !bytes.Any())
                return Bucket.Empty;

            return new MemoryBucket(bytes.ToArray());
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

        public static Bucket AsBucket(this IEnumerable<byte[]> bytes)
        {
            if (bytes is null || !bytes.Any())
                return Bucket.Empty;

            return bytes.Select(x => x.AsBucket()).AsBucket();
        }

        public static Bucket AsBucket(this IEnumerable<byte[]> bytes, bool keepOpen)
        {
            if (bytes is null || !bytes.Any())
                return Bucket.Empty;

            return bytes.Select(x => x.AsBucket()).AsBucket(keepOpen);
        }

        public static Bucket AsBucket(this ReadOnlyMemory<byte> memory)
        {
            return new MemoryBucket(memory);
        }

        public static Bucket AsBucket(this IEnumerable<Bucket> buckets)
        {
            if (buckets is null || !buckets.Any())
                return Bucket.Empty;

            return new AggregateBucket(buckets.ToArray());
        }

        public static Bucket AsBucket(this IEnumerable<ReadOnlyMemory<byte>> buffers)
        {
            if (buffers is null || !buffers.Any())
                return Bucket.Empty;

            return new AggregateBucket(buffers.Select(x=>x.AsBucket()).ToArray());
        }

        public static Bucket AsBucket(this IEnumerable<ReadOnlyMemory<byte>> buffers, bool keepOpen)
        {
            if (buffers is null || !buffers.Any())
                return Bucket.Empty;

            return new AggregateBucket(keepOpen, buffers.Select(x => x.AsBucket()).ToArray());
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
            return Compress(bucket, algorithm, BucketCompressionLevel.Default);
        }

        public static Bucket Compress(this Bucket bucket, BucketCompressionAlgorithm algorithm, BucketCompressionLevel level)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            switch (algorithm)
            {
                case BucketCompressionAlgorithm.ZLib:
                case BucketCompressionAlgorithm.Deflate:
                    return new ZLibBucket(bucket, algorithm, CompressionMode.Compress, level);
                case BucketCompressionAlgorithm.GZip:
                    // Could be optimized like zlib, but currently unneeded
                    return new CompressionBucket(bucket, (inner) => new GZipStream(inner, level switch
                    {
                        BucketCompressionLevel.Store => CompressionLevel.NoCompression,
                        BucketCompressionLevel.BestSpeed => CompressionLevel.Fastest,
#if NET6_0_OR_GREATER
                        BucketCompressionLevel.Maximum => CompressionLevel.SmallestSize,
#endif
                        _ => CompressionLevel.Optimal,
                    }));
                case BucketCompressionAlgorithm.Brotli:
#if !NETFRAMEWORK
                    // Available starting with .Net Core
                    return new CompressionBucket(bucket, (inner) => new BrotliStream(inner, level switch
                    {
                        BucketCompressionLevel.Store => CompressionLevel.NoCompression,
                        BucketCompressionLevel.BestSpeed => CompressionLevel.Fastest,
#if NET6_0_OR_GREATER
                        BucketCompressionLevel.Maximum => CompressionLevel.SmallestSize,
#endif
                        _ => CompressionLevel.Optimal,
                    }));
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
                await bucket.WriteToAsync(ms).ConfigureAwait(false);

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
                foreach (var b in ro)
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

        //public static async ValueTask<BucketBytes> ReadUntilAsync(this Bucket bucket, Func<byte, bool> predicate, int pollSize = 1, int maxRequested = int.MaxValue)
        //{
        //    if (bucket is null)
        //        throw new ArgumentNullException(nameof(bucket));
        //
        //    var bb = await bucket.PollAsync(pollSize).ConfigureAwait(false);
        //
        //    if (bb.IsEof)
        //        return bb;
        //
        //    int n = bb.Span.IndexOf(predicate);
        //
        //    return await bucket.ReadAsync(n >= 0 ? n + 1 : bb.Length + 1).ConfigureAwait(false);
        //}


        public static bool All<T>(this ReadOnlySpan<T> span, Func<T, bool> predicate)
            where T : struct
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            foreach (var i in span)
            {
                if (!predicate(i))
                    return false;
            }
            return true;
        }

        public static bool Any<T>(this ReadOnlySpan<T> span, Func<T, bool> predicate)
            where T : struct
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

        public static int IndexOf<T>(this ReadOnlySpan<T> span, Func<T, bool> predicate)
            where T : struct
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            for (int i = 0; i < span.Length; i++)
            {
                if (predicate(span[i]))
                    return i;
            }
            return -1;
        }
    }
}
