using System;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Signatures
{
    sealed class OpenPgpContainer : WrappingBucket
    {
        bool _notFirst;
        bool _isSsh;
        bool _reading;
        bool _isDer;

        public OpenPgpContainer(Bucket inner) : base(inner)
        {
        }

        public bool IsSsh => _isSsh;

        public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
        {
            while (true)
            {
                var (bucket, _) = await ReadPacketAsync().ConfigureAwait(false);

                if (bucket is null)
                    return BucketBytes.Eof;
            }
        }

        public async ValueTask<(Bucket? Bucket, OpenPgpTagType Type)> ReadPacketAsync()
        {
            if (_reading)
                throw new InvalidOperationException();
            var first = false;
            var inner = Inner;
            bool sshPublicKey = false;
            if (!_notFirst)
            {
                var didRead = false;
                var bb = await Inner.PollAsync().ConfigureAwait(false);

                if (bb.Length < 6)
                {
                    bb = await Inner.ReadExactlyAsync(6).ConfigureAwait(false);
                    didRead = true;
                }

                if (bb.StartsWithASCII("SSHSIG"))
                {
                    _isSsh = true;
                    if (!didRead)
                        bb = await Inner.ReadExactlyAsync(6).ConfigureAwait(false);
                }
                else if (bb.Span.StartsWith(new byte[] { 0x00, 0x00, 0x00 }))
                {
                    if (didRead)
                        inner = bb.ToArray().AsBucket() + Inner;

                    _isSsh = true;
                    sshPublicKey = true;
                }
                else if (await DerBucket.BytesMayBeDerAsync(bb).ConfigureAwait(false))
                {
                    _isDer = true;
                    if (didRead)
                        inner = bb.ToArray().AsBucket() + Inner;
                }
                else
                {
                    if (didRead)
                        inner = bb.ToArray().AsBucket() + Inner;
                }
                _notFirst = true;
                first = true;
            }

            if (_isSsh)
            {
                if (first)
                {
                    return (inner.NoDispose(), sshPublicKey ? OpenPgpTagType.PublicKey : OpenPgpTagType.Signature);
                }
                else
                {
                    await Inner.ReadUntilEofAsync().ConfigureAwait(false);
                    return (null, default);
                }
            }
            else if (_isDer)
            {
                if (first)
                {
                    return (new DerBucket(Inner.NoDispose()), OpenPgpTagType.DerValue);
                }
                else
                {
                    await Inner.ReadUntilEofAsync().ConfigureAwait(false);
                    return (null, default);
                }
            }
            else
            {
                var bq = await inner.ReadByteAsync().ConfigureAwait(false);

                if (bq is null)
                    return (null, default);

                var b = bq.Value;
                bool oldFormat;
                OpenPgpTagType tag;
                uint remaining = 0;

                if ((b & 0x80) == 0)
                    throw new BucketException("Bad packet");

                oldFormat = 0 == (b & 0x40);
                if (oldFormat)
                {
                    tag = (OpenPgpTagType)((b & 0x3c) >> 2);
                    remaining = (uint)(b & 0x3);
                }
                else
                    tag = (OpenPgpTagType)(b & 0x3F);

                if (!oldFormat)
                {
                    var len = await ReadLengthAsync(inner).ConfigureAwait(false) ?? throw new BucketEofException(Inner);

                    remaining = len;
                }
                else if (remaining == 3)
                {
                    // Indetermined size, upto end
                    return (inner.NoDispose().AtEof(() => _reading = false), tag);
                }
                else
                {
                    remaining = remaining switch
                    {
                        0 => await inner.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(Inner),
                        1 => await inner.ReadNetworkUInt16Async().ConfigureAwait(false),
                        2 => await inner.ReadNetworkUInt32Async().ConfigureAwait(false),
                        _ => throw new NotImplementedException("Indetermined size"),
                    };
                }

                _reading = true;
                return (inner.NoDispose().TakeExactly(remaining).AtEof(() => _reading = false), tag);
            }
        }

        public static async ValueTask<uint?> ReadLengthAsync(Bucket bucket)
        {
            var b = await bucket.ReadByteAsync().ConfigureAwait(false);

            if (!b.HasValue)
                return null;

            if (b < 192)
                return b;

            else if (b < 224)
            {
                var b2 = await bucket.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(bucket);

                return (uint)((b - 192 << 8) + b2 + 192);
            }
            else if (b == 255)
            {
                return await bucket.ReadNetworkUInt32Async().ConfigureAwait(false);
            }
            else
                throw new NotImplementedException("Partial lengths");
        }
    }
}


