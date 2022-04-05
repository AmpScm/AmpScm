﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    public static class SpecializedBucketExtensions
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

        public static async ValueTask<(BucketBytes, BucketEol)> ReadUntilEolFullAsync(this Bucket bucket, BucketEol acceptableEols, BucketEolState? eolState = null, int requested = int.MaxValue)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            IEnumerable<byte>? result = null;

            int rq = requested;
            if (eolState?._kept.HasValue ?? false)
            {
                var kept = eolState._kept!.Value;
                eolState._kept = null;

                switch (kept)
                {
                    case (byte)'\n' when (0 != (acceptableEols & BucketEol.LF)):
                        return (new BucketBytes(new[] { kept }, 0, 1), BucketEol.LF);
                    case (byte)'\r' when (0 != (acceptableEols & BucketEol.CR) && (acceptableEols & BucketEol.CRLF) == 0):
                        return (new BucketBytes(new[] { kept }, 0, 1), BucketEol.CR);
                    case (byte)'\0' when (0 != (acceptableEols & BucketEol.Zero)):
                        return (new BucketBytes(new[] { kept }, 0, 1), BucketEol.Zero);
                    case (byte)'\r' when (0 != (acceptableEols & BucketEol.CRLF)):
                        rq = 1;
                        goto default;
                    default:
                        result = new[] { kept };
                        break;
                }
            }
            else if ((BucketEol.CRLF | BucketEol.CR) == (acceptableEols & (BucketEol.CRLF | BucketEol.CR)) && eolState is null)
            {
                throw new ArgumentNullException(nameof(eolState));
            }
            while (true)
            {
                BucketBytes bb;
                BucketEol eol;

                (bb, eol) = await bucket.ReadUntilEolAsync(acceptableEols, rq).ConfigureAwait(false);
                rq = requested;

                if (bb.IsEof)
                    return ((result != null) ? result.ToArray() : bb, eol);
                else if (bb.IsEmpty)
                    throw new InvalidOperationException();
                else if ((acceptableEols & BucketEol.CRLF) != 0 && result is byte[] rb && rb.Length == 1 && rb[0] == '\r')
                {
                    if (bb[0] == '\n')
                        return (result.Concat(bb.ToArray()).ToArray(), BucketEol.CRLF);
                    else if ((acceptableEols & BucketEol.CR) != 0)
                    {
                        eolState!._kept = bb[0];
                        return (new[] { (byte)'\r' }, BucketEol.CR);
                    }
                    else if (eol != BucketEol.None)
                        return (result.Concat(bb.ToArray()).ToArray(), eol); // '\0' case
                }
                else if (result == null && eol != BucketEol.None && eol != BucketEol.CRSplit)
                    return (bb, eol);

                requested -= bb.Length;

                if (result == null)
                    result = bb.ToArray();
                else
                    result = result.Concat(bb.ToArray());

                if (requested == 0)
                {
                    return ((result as byte[]) ?? result.ToArray(), eol);
                }
                else if (eol == BucketEol.CRSplit)
                {
                    // Bad case. We may have a \r that might be a \n

                    var poll = await bucket.PollReadAsync(1).ConfigureAwait(false);

                    if (!poll.Data.IsEmpty)
                    {
                        var b = poll[0];
                        await poll.Consume(1).ConfigureAwait(false);

                        if (b == '\n')
                        {
                            // Phew, we were lucky. We got a \r\n
                            result = result.Concat(new byte[] { poll[0] });
                            return (result.ToArray(), BucketEol.CRLF);
                        }
                        else if (0 != (acceptableEols & BucketEol.CR))
                        {
                            // We return the part ending with '\r'
                            eolState!._kept = b; // And keep the next byte
                            return (result.ToArray(), BucketEol.CR);
                        }
                        else if (b == '\0' && 0 != (acceptableEols & BucketEol.Zero))
                        {
                            // Another edge case :(
                            result = result.Concat(new byte[] { poll[0] });
                            return (result.ToArray(), BucketEol.Zero);
                        }
                        else
                        {
                            result = result.Concat(new byte[] { b }).ToArray();
                            continue;
                        }
                    }
                    else
                    {
                        // We are at eof, so no issues with future reads
                        eol = (0 != (acceptableEols & BucketEol.CR) ? BucketEol.CR : BucketEol.None);

                        return (result.ToArray(), eol);
                    }
                }
                else if (eol == BucketEol.None)
                    continue;
                else
                {
                    return ((result as byte[]) ?? result.ToArray(), eol);
                }
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

        public static async ValueTask<BucketBytes> ReadUntilAsync(this Bucket bucket, byte b)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            IEnumerable<byte>? result = null;

            while (true)
            {
                using var poll = await bucket.PollReadAsync().ConfigureAwait(false);

                if (poll.Data.IsEof)
                    return (result != null) ? new BucketBytes(result.ToArray()) : poll.Data;

                for (int i = 0; i < poll.Data.Length; i++)
                {
                    if (poll[i] == b)
                    {
                        BucketBytes r;
                        if (result == null)
                            r = poll.Data.Slice(0, i + 1).ToArray(); // Make copy, as data is transient
                        else
                            r = result.Concat(poll.Data.Slice(0, i + 1).ToArray()).ToArray();

                        await poll.Consume(i + 1).ConfigureAwait(false);
                        return r;
                    }
                }

                var extra = poll.Data.ToArray();
                if (result == null)
                    result = extra;
                else
                    result = result.Concat(extra);

                await poll.Consume(poll.Length).ConfigureAwait(false);
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


        /// <summary>
        /// Read in-memory buckets combined in a single buffer
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="bufferSize"></param>
        /// <param name="requested"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static async ValueTask<BucketBytes> ReadCombinedAsync(this Bucket bucket, int bufferSize, int requested = int.MaxValue)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (bucket is IBucketReadBuffers iov)
            {
                var r = await iov.ReadBuffersAsync(bufferSize).ConfigureAwait(false);

                int bytes = (r.Buffers.Length > 0) ? r.Buffers.Sum(x => x.Length) : 0;

                if (bytes > 0)
                {
                    byte[] buffer = new byte[bytes];
                    int pos = 0;

                    foreach (var v in r.Buffers)
                    {
                        v.CopyTo(new Memory<byte>(buffer, pos, v.Length));
                        pos += v.Length;
                    }

                    return buffer;
                }
            }

            return await bucket.ReadAsync(requested).ConfigureAwait(false);
        }

        public static async ValueTask<(ReadOnlyMemory<byte>[] Buffers, bool Done)> ReadBuffersAsync(this Bucket bucket, int maxRequested = int.MaxValue)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (bucket is IBucketReadBuffers iov)
                return await iov.ReadBuffersAsync(maxRequested).ConfigureAwait(false);

            var bb = await bucket.ReadAsync(maxRequested).ConfigureAwait(false);

            if (bb.IsEof)
                return (Array.Empty<ReadOnlyMemory<byte>>(), true);
            else
                return (new[] { bb.Memory }, false);
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

