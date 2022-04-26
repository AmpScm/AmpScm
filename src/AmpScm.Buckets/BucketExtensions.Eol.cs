using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets
{
    public static partial class BucketExtensions
    {
#if !NETFRAMEWORK
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
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

                if (eol != BucketEol.None && eol != BucketEol.CRSplit && result is null)
                    return (bb, eol);
                else if (bb.IsEmpty)
                {
                    if (bb.IsEof)
                        return ((result != null) ? result.ToArray() : bb, eol);
                    else
                        throw new InvalidOperationException();
                }
                else if (rq == 1 && (acceptableEols & BucketEol.CRLF) != 0 && result is byte[] rb && rb.Length == 1 && rb[0] == '\r')
                {
                    if (bb[0] == '\n')
                        return (result.AppendBytes(bb), BucketEol.CRLF);
                    else if ((acceptableEols & BucketEol.CR) != 0)
                    {
                        eolState!._kept = bb[0];
                        return (new[] { (byte)'\r' }, BucketEol.CR);
                    }
                    else if (eol != BucketEol.None)
                        return (result.AppendBytes(bb), eol); // '\0' case
                }

                rq = (requested -= bb.Length);
                if (requested == 0)
                {
                    if (result is null)
                        return (bb, eol);

                    return (result.AppendBytes(bb), eol);
                }

                if (result == null)
                    result = bb.ToArray();
                else
                    result = result.Concat(bb.ToArray());

                if (eol == BucketEol.CRSplit)
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
                            result = result.Concat(new byte[] { b });
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
                            r = result.AppendBytes(poll.Data.Slice(0, i + 1));

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
                var (Buffers, Done) = await iov.ReadBuffersAsync(bufferSize).ConfigureAwait(false);

                int bytes = (Buffers.Length > 0) ? Buffers.Sum(x => x.Length) : 0;

                if (bytes > 0)
                {
                    byte[] buffer = new byte[bytes];
                    int pos = 0;

                    foreach (var v in Buffers)
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
    }
}

