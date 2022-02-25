﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    public static class SpecializedBucketExtensions
    {
        public static CreateHashBucket SHA1(this Bucket self, Action<byte[]> created)
        {
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
            return new CreateHashBucket(self, System.Security.Cryptography.SHA1.Create(), created);
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
        }

        public static CreateHashBucket SHA256(this Bucket self, Action<byte[]> created)
        {
            return new CreateHashBucket(self, System.Security.Cryptography.SHA256.Create(), created);
        }

        public static CreateHashBucket MD5(this Bucket self, Action<byte[]> created)
        {
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
            return new CreateHashBucket(self, System.Security.Cryptography.MD5.Create(), created);
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
        }

        public static CreateHashBucket Crc32(this Bucket self, Action<int> created)
        {
            return new CreateHashBucket(self, CreateHashBucket.Crc32.Create(), (v) => created(NetBitConverter.ToInt32(v, 0)));
        }

        public async static ValueTask<BucketBytes> ReadFullAsync(this Bucket self, int requested)
        {
            if (self is null)
                throw new ArgumentNullException(nameof(self));

            IEnumerable<byte>? result = null;

            while (true)
            {
                var bb = await self.ReadAsync(requested).ConfigureAwait(false);

                if (bb.IsEof)
                    return (result != null) ? result.ToArray() : bb;

                requested -= bb.Length;

                if (result == null)
                    result = bb.ToArray();
                else
                    result = result.Concat(bb.ToArray());

                if (requested == 0)
                {
                    return (result as byte[]) ?? result.ToArray();
                }
            }
        }

        public async static ValueTask<(BucketBytes, BucketEol)> ReadUntilEolFullAsync(this Bucket self, BucketEol acceptableEols, BucketEolState? eolState, int requested = int.MaxValue)
        {
            if (self is null)
                throw new ArgumentNullException(nameof(self));

            IEnumerable<byte>? result = null;

            if (eolState?._kept.HasValue ?? false)
            {
                var kept = eolState._kept!.Value;
                eolState._kept = null;

                switch (kept)
                {
                    case (byte)'\r' when (0 != (acceptableEols & BucketEol.CR)):
                        return (new BucketBytes(new[] { kept }, 0, 1), BucketEol.CR);
                    case (byte)'\0' when (0 != (acceptableEols & BucketEol.Zero)):
                        return (new BucketBytes(new[] { kept }, 0, 1), BucketEol.Zero);
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

                (bb, eol) = await self.ReadUntilEolAsync(acceptableEols).ConfigureAwait(false);

                if (bb.IsEof)
                    return ((result != null) ? result.ToArray() : bb, eol);
                else if (bb.IsEmpty)
                    throw new InvalidOperationException();
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
                        result = result.Concat(new byte[] { bb[0] }).ToArray();

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

        public async static ValueTask<BucketBytes> ReadUntilAsync(this Bucket self, byte b)
        {
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

        public static int CharCount(this BucketEol eol)
            => eol switch
            {
                BucketEol.CRLF => 2,
                BucketEol.None => 0,
                _ => 1,
            };
    }
}
