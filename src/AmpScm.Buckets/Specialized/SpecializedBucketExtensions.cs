using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    public static class SpecializedBucketExtensions
    {
        public static Bucket SHA1(this Bucket self, Action<byte[]> created)
        {
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
            return new CreateHashBucket(self, System.Security.Cryptography.SHA1.Create(), created);
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
        }

        public static Bucket SHA256(this Bucket self, Action<byte[]> created)
        {
            return new CreateHashBucket(self, System.Security.Cryptography.SHA256.Create(), created);
        }

        public static Bucket MD5(this Bucket self, Action<byte[]> created)
        {
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
            return new CreateHashBucket(self, System.Security.Cryptography.MD5.Create(), created);
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
        }

        public static Bucket Crc32(this Bucket self, Action<int> created)
        {
            return new CreateHashBucket(self, CreateHashBucket.Crc32.Create(), (v) => created(NetBitConverter.ToInt32(v, 0)));
        }

        public static Bucket ReadLength(this Bucket self, Action<long> bytesRead)
        {
            return new CreateHashBucket(self, CreateHashBucket.BytesRead.Create(), (v) => bytesRead(BitConverter.ToInt64(v, 0)));
        }

        public static async ValueTask ReadSkipUntilEofAsync(this Bucket self)
        {
            if (self == null)
                throw new ArgumentNullException(nameof(self));

            while (0 != await self.ReadSkipAsync(int.MaxValue).ConfigureAwait(false))
            { }
        }

        public static async ValueTask<BucketBytes> ReadFullAsync(this Bucket self, int requested)
        {
            if (self is null)
                throw new ArgumentNullException(nameof(self));

            byte[]? resultBuffer = null;
            int collected = 0;
            while (true)
            {
                var bb = await self.ReadAsync(requested).ConfigureAwait(false);

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

        public static async ValueTask<(BucketBytes, BucketEol)> ReadUntilEolFullAsync(this Bucket self, BucketEol acceptableEols, BucketEolState? eolState = null, int requested = int.MaxValue)
        {
            if (self is null)
                throw new ArgumentNullException(nameof(self));

            IEnumerable<byte>? result = null;

            int rq = requested;
            BucketEol acc = acceptableEols;
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
                    case (byte)'\r':
                        rq = 1;
                        goto default;
                    default:
                        result = new[] { kept };
                        break;
                }
            }
            else if (0 != (acceptableEols & BucketEol.CRLF) && eolState == null)
            {
                throw new ArgumentNullException(nameof(eolState));
            }
            while (true)
            {
                BucketBytes bb;
                BucketEol eol;

                (bb, eol) = await self.ReadUntilEolAsync(acceptableEols, rq).ConfigureAwait(false);
                rq = requested;
                acc = acceptableEols;

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

                    var poll = await self.PollReadAsync(1).ConfigureAwait(false);

                    if (!poll.Data.IsEmpty && poll[0] == '\n')
                    {
                        // Phew, we were lucky. We got a \r\n
                        result = result.Concat(new byte[] { poll[0] });

                        await poll.Consume(1).ConfigureAwait(false);

                        return (result.ToArray(), BucketEol.CRLF);
                    }
                    else if (!poll.Data.IsEmpty)
                    {
                        // We got something else
                        if (0 != (acceptableEols & BucketEol.CR))
                        {
                            // Keep the next byte for the next read :(
                            eolState!._kept = poll[0];
                            await poll.Consume(1).ConfigureAwait(false);
                            return (result.ToArray(), BucketEol.CR);
                        }
                        else
                        {
                            await poll.Consume(1).ConfigureAwait(false);
                            result = result.Concat(new byte[] { poll[0] }).ToArray();
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

        public static async ValueTask<BucketBytes> ReadUntilAsync(this Bucket self, byte b)
        {
            if (self is null)
                throw new ArgumentNullException(nameof(self));
            IEnumerable<byte>? result = null;

            while (true)
            {
                using var poll = await self.PollReadAsync().ConfigureAwait(false);

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

        public static async ValueTask<int> ReadNetworkInt32Async(this Bucket self)
        {
            var bb = await self.ReadFullAsync(sizeof(int)).ConfigureAwait(false);

            if (bb.Length != sizeof(uint))
                throw new BucketException($"Unexpected EOF while reading from {self.Name} bucket");

            return NetBitConverter.ToInt32(bb, 0);
        }


        public static async ValueTask<byte?> ReadByteAsync(this Bucket self)
        {
            if (self is null)
                throw new ArgumentNullException(nameof(self));
            var bb = await self.ReadAsync(1).ConfigureAwait(false);

            if (bb.Length != 1)
                return null;

            return bb[0];
        }

        [CLSCompliant(false)]
        public static async ValueTask<uint> ReadNetworkUInt32Async(this Bucket self)
        {
            var bb = await self.ReadFullAsync(sizeof(uint)).ConfigureAwait(false);

            if (bb.Length != sizeof(uint))
                throw new BucketException($"Unexpected EOF while reading from {self.Name} bucket");

            return NetBitConverter.ToUInt32(bb, 0);
        }

        public static async ValueTask<long> ReadNetworkInt64Async(this Bucket self)
        {
            var bb = await self.ReadFullAsync(sizeof(long)).ConfigureAwait(false);

            if (bb.Length != sizeof(ulong))
                throw new BucketException($"Unexpected EOF while reading from {self.Name} bucket");

            return NetBitConverter.ToInt64(bb, 0);
        }

        [CLSCompliant(false)]
        public static async ValueTask<ulong> ReadNetworkUInt64Async(this Bucket self)
        {
            var bb = await self.ReadFullAsync(sizeof(ulong)).ConfigureAwait(false);

            if (bb.Length != sizeof(ulong))
                throw new BucketException($"Unexpected EOF while reading from {self.Name} bucket");

            return NetBitConverter.ToUInt64(bb, 0);
        }


        /// <summary>
        /// Read in-memory buckets combined in a single buffer
        /// </summary>
        /// <param name="self"></param>
        /// <param name="bufferSize"></param>
        /// <param name="maxRequested"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static async ValueTask<BucketBytes> ReadCombinedAsync(this Bucket self, int bufferSize, int maxRequested = int.MaxValue)
        {
            if (self is null)
                throw new ArgumentNullException(nameof(self));
            else if (self is IBucketReadBuffers iov)
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

            return await self.ReadAsync(maxRequested).ConfigureAwait(false);
        }
    }
}

