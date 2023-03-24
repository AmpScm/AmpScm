﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Signatures
{
    internal enum DerType : byte
    {
        Eof = 0,
        Boolean = 1,
        Integer = 2,
        BitString = 3,
        OctetString = 4,
        Null = 5,
        ObjectIdentifier = 6,
        ObjectDescriptor = 7,
        External = 8,
        Real = 9,
        Enumerated = 10,
        EmbeddedPdv = 11,
        Utf8String = 12,
        RelativeOid = 13,
        Time = 14,
        // 15 = reserved
        Sequence = 16,
        Set = 17,
        NummericString = 18,
        PrintableString = 19,
        T61String = 20,
    }

    internal sealed class DerBucket : WrappingBucket
    {
        private bool _eof;
        private bool _reading;

        public DerBucket(Bucket inner) : base(inner)
        {
        }

        internal static async ValueTask<bool> BytesMayBeDerAsync(BucketBytes bb)
        {
            if (bb.IsEmpty || bb[0] != 0x30) // 0x30 = start of 'sequence'
                return false;
            try
            {
                var d = bb.ToArray().AsBucket();

                using var der = new DerBucket(d);

                var r = await der.ReadValueAsync().ConfigureAwait(false);

                if (r.Bucket is null)
                    return false;

                if (r.Type == DerType.Sequence)
                    return true;

                return false;
            }
            catch (BucketException)
            {
                return false;
            }
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
        {
            Bucket? b;
            do
            {
                (b, _) = await ReadValueAsync().ConfigureAwait(false);

                if (b != null)
                    await b.ReadUntilEofAndCloseAsync().ConfigureAwait(false);
            }
            while (b != null);

            return BucketBytes.Eof;
        }

        public async ValueTask<(Bucket? Bucket, DerType Type)> ReadValueAsync()
        {
            if (_reading)
                throw new InvalidOperationException();
            else if (_eof)
                return (null, DerType.Eof);

            var bb = await Inner.ReadAsync(1).ConfigureAwait(false);

            if (bb.IsEmpty)
                return (null, DerType.Eof);

            byte b = bb[0];
            int type;

            if ((b & 0x1F) == 0)
            {
                _eof = true;
                return (null, DerType.Eof);
            }
            else if ((b & 0x1F) == 0x1F)
            {
                bb = await Inner.ReadAsync(1).ConfigureAwait(false);

                type = 0;
                do
                {
                    if (bb.IsEmpty)
                        throw new BucketEofException(Inner);

                    type <<= 7;
                    type |= bb[0] & 0x7F;
                }
                while (bb[0] >= 0x80);
            }
            else
                type = (b & 0x1F);

            bool isPrimitive = (b & 0x40) != 0;
            int tagClass = b >> 6;

            bb = await Inner.ReadAsync(1).ConfigureAwait(false);

            if (bb.IsEmpty)
                throw new BucketEofException(Inner);
            b = bb[0];
            if ((b & 0x80) == 0)
            {
                _reading = true;
                return (Inner.NoDispose().TakeExactly(b).AtEof(() => _reading = false), (DerType)type);
            }
            else if (b > 0x80 && b < 0xFF)
            {
                long len = 0;

                bb = await Inner.ReadExactlyAsync(b - 128).ConfigureAwait(false);
                if (bb.Length != b - 0x80)
                    throw new BucketEofException(Inner);

                for (int i = 0; i < bb.Length; i++)
                {
                    len <<= 8;
                    len |= bb[i];
                }

                _reading = true;
                return (Inner.NoDispose().TakeExactly(len).AtEof(() => _reading = false), (DerType)type);
            }
            else
                throw new InvalidOperationException("Unsupported DER form");
        }
    }
}
