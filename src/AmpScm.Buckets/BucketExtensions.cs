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
using System.Diagnostics;
using System.Collections.Concurrent;

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
                return new AggregateBucket.SimpleAggregate(bucket, newLast);
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
                return new AggregateBucket.SimpleAggregate(newFirst, bucket);
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
        /// 
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="requested"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static async ValueTask<BucketBytes> ReadExactlyAsync(this Bucket bucket, int requested)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (requested <= 0 || requested > Bucket.MaxRead)
                throw new ArgumentOutOfRangeException(nameof(requested), requested, null);

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
        /// Reads data with exact blockSize
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="blockSize"></param>
        /// <param name="requested"></param>
        /// <param name="failOnPartial"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static async ValueTask<BucketBytes> ReadBlocksAsync(this Bucket bucket, int blockSize, int requested, bool failOnPartial = true)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (blockSize < 2)
                throw new ArgumentOutOfRangeException(nameof(blockSize), blockSize, null);
            else if (requested < blockSize || requested > Bucket.MaxRead)
                throw new ArgumentOutOfRangeException(nameof(requested), requested, null);

            var bb = bucket.Peek();

            if (bb.Length >= blockSize)
            {
                int fetch = bb.Length - bb.Length % blockSize;

                Debug.Assert(fetch > 0 && fetch >= blockSize && fetch <= requested);

                bb = await bucket.ReadAsync(fetch).ConfigureAwait(false);

                Debug.Assert(bb.Length > 0 && bb.Length % blockSize == 0);

                return bb;
            }
            else
            {
                bb = await bucket.ReadExactlyAsync(blockSize).ConfigureAwait(false);

                if (bb.Length % blockSize != 0)
                    throw new BucketException();

                return bb;
            }
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
        public static Bucket TakeExactly(this Bucket bucket, long length, bool alwaysWrap = false)
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
        /// <remarks>See also <see cref="TakeExactly"/>, which takes an exact amount
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
        public static Bucket SkipExactly(this Bucket bucket, long skipBytes, bool alwaysWrap = false)
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

        /// <summary>
        /// Creates a bucket that reads all bytes expect the final <paramref name="leave"/> bytes from a bucket,
        /// and calls the callback with the final bytes before returning EOF for the first time
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="leave"></param>
        /// <param name="left"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Bucket Leave(this Bucket bucket, int leave, Func<BucketBytes, long, ValueTask> left)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (leave < 1)
                throw new ArgumentOutOfRangeException(nameof(leave), leave, message: null);
            else if (left is null)
                throw new ArgumentNullException(nameof(left));

            return new LeaveBucket(bucket, leave, left);
        }

        /// <summary>
        /// Creates a bucket that reads all bytes expect the final <paramref name="leave"/> bytes from a bucket,
        /// and calls the callback with the final bytes before returning EOF for the first time
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="leave"></param>
        /// <param name="left"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Bucket Leave(this Bucket bucket, int leave, Action<BucketBytes, long> left)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (leave < 1)
                throw new ArgumentOutOfRangeException(nameof(leave), leave, message: null);
            else if (left is null)
                throw new ArgumentNullException(nameof(left));

            return new LeaveBucket(bucket, leave, (lft, length) => { left(lft, length); return new(); });
        }

        public static Bucket NoDispose(this Bucket bucket, bool alwaysWrap = false)
        {
            if (!alwaysWrap && bucket is IBucketNoDispose nc)
                return nc.NoDispose();
            else
                return new NoDisposeBucket(bucket);
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
#pragma warning disable CA1851 // Possible multiple enumerations of 'IEnumerable' collection
            if (bytes is null || (bytes as byte[])?.Length == 0 || !bytes.Any())
                return Bucket.Empty;

            return new MemoryBucket(bytes as byte[] ?? bytes.ToArray());
#pragma warning restore CA1851 // Possible multiple enumerations of 'IEnumerable' collection
        }

        public static Bucket AsBucket(this byte[] bytes, bool copy)
        {
            if ((bytes?.Length ?? 0) == 0)
                return Bucket.Empty;

            if (copy)
            {
                byte[] data = new byte[bytes!.Length];
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

        public static Bucket AsBucket(this Memory<byte> memory)
        {
            return new MemoryBucket(memory);
        }

        public static Bucket AsBucket(this IEnumerable<Bucket> buckets)
        {
#pragma warning disable CA1851 // Possible multiple enumerations of 'IEnumerable' collection
            if (buckets is null || !buckets.Any())
                return Bucket.Empty;

            return buckets.ToArray();
#pragma warning restore CA1851 // Possible multiple enumerations of 'IEnumerable' collection
        }

        public static Bucket AsBucket(this IEnumerable<ReadOnlyMemory<byte>> buffers)
        {
#pragma warning disable CA1851 // Possible multiple enumerations of 'IEnumerable' collection
            if (buffers is null || !buffers.Any())
                return Bucket.Empty;

            return buffers.Select(x => x.AsBucket()).ToArray();
#pragma warning restore CA1851 // Possible multiple enumerations of 'IEnumerable' collection
        }

        public static Bucket AsBucket(this IEnumerable<ReadOnlyMemory<byte>> buffers, bool keepOpen)
        {
#pragma warning disable CA1851 // Possible multiple enumerations of 'IEnumerable' collection
            if (buffers is null || !buffers.Any())
                return Bucket.Empty;

            var arr = buffers.Select(x => x.AsBucket()).ToArray();

            if (arr.Length == 1)
                return arr[0];
            else if (keepOpen)
                return new AggregateBucket.SimpleAggregate(keepOpen, arr);
            else
                return arr;
#pragma warning restore CA1851 // Possible multiple enumerations of 'IEnumerable' collection
        }

        public static Bucket AsBucket(this IEnumerable<Bucket> buckets, bool keepOpen)
        {
#pragma warning disable CA1851 // Possible multiple enumerations of 'IEnumerable' collection
            if (buckets is null || !buckets.Any())
                return Bucket.Empty;
            else if (!keepOpen)
                return Bucket.FromBucketArray(buckets as Bucket[] ?? buckets.ToArray());

            var arr = buckets.ToArray();

            if (arr.Length == 0)
                return Bucket.Empty;
            else if (arr.Length == 1)
                return arr[0];
            else if (!keepOpen)
                return arr;
            else
                return new AggregateBucket.SimpleAggregate(keepOpen, arr);
#pragma warning restore CA1851 // Possible multiple enumerations of 'IEnumerable' collection
        }

        public static Bucket Decompress(this Bucket bucket, BucketCompressionAlgorithm algorithm)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            switch (algorithm)
            {
                case BucketCompressionAlgorithm.ZLib:
                case BucketCompressionAlgorithm.Deflate:
                    return new ZLibBucket(bucket, algorithm, CompressionMode.Decompress);
                case BucketCompressionAlgorithm.GZip:
                    return new GZipBucket(bucket, CompressionMode.Decompress);
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
                foreach (byte b in ro)
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


        public static async ValueTask<bool> HasSameContentsAsync(this Bucket left, Bucket right)
        {
            if (left is null)
                throw new ArgumentNullException(nameof(left));
            else if (right is null)
                throw new ArgumentNullException(nameof(right));

            using (left)
            using (right)

                while (true)
                {
                    BucketBytes bbLeft, bbRight;

                    bbLeft = await left.ReadAsync().ConfigureAwait(false);

                    if (bbLeft.IsEof)
                    {
                        bbRight = await right.ReadAsync(1).ConfigureAwait(false);

                        if (!bbRight.IsEof)
                            return false;
                        else
                            return true;
                    }

                    do
                    {
                        bbRight = await right.ReadAsync(bbLeft.Length).ConfigureAwait(false);

                        if (bbRight.IsEof)
                            return false;

                        if (!bbRight.Span.SequenceEqual(bbLeft.Span.Slice(0, bbRight.Length)))
                            return false;

                        bbLeft = bbLeft.Slice(bbRight.Length);
                    }
                    while (bbLeft.Length > 0);
                }
        }

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

        public static bool SequenceEqual(this ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right)
        {
            return left.Span.SequenceEqual(right.Span);
        }

        public static string HashToString(this ReadOnlyMemory<byte> bytes)
        {
            return string.Join("", Enumerable.Range(1, bytes.Length - 1).Select(i => bytes.Span[i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture)));
        }
    }
}
