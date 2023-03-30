using System;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Cryptography
{
    internal sealed class OpenPgpContainer : WrappingBucket
    {
        private bool _notFirst;
        private bool _isSsh;
        private bool _reading;
        private bool _isDer;

        public OpenPgpContainer(Bucket source) : base(source)
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
                throw new BucketException("Can't obtain new packet, when the last one is not completely read");

            bool first = false;
            var inner = Source;
            bool sshPublicKey = false;
            if (!_notFirst)
            {
                bool didRead = false;
                var bb = await Source.PollAsync().ConfigureAwait(false);

                if (bb.Length < 6)
                {
                    bb = await Source.ReadExactlyAsync(6).ConfigureAwait(false);
                    didRead = true;
                }

                if (bb.StartsWithASCII("SSHSIG"))
                {
                    _isSsh = true;
                    if (!didRead)
                        bb = await Source.ReadExactlyAsync(6).ConfigureAwait(false);
                }
                else if (bb.Span.StartsWith(new byte[] { 0x00, 0x00, 0x00 }))
                {
                    if (didRead)
                        inner = bb.ToArray().AsBucket() + Source;

                    _isSsh = true;
                    sshPublicKey = true;
                }
                else if (await DerBucket.BytesMayBeDerAsync(bb).ConfigureAwait(false))
                {
                    _isDer = true;
                    if (didRead)
                        inner = bb.ToArray().AsBucket() + Source;
                }
                else
                {
                    if (didRead)
                        inner = bb.ToArray().AsBucket() + Source;
                }
                _notFirst = true;
                first = true;
            }

            if (_isSsh)
            {
                if (first)
                {
                    return (inner.NoDispose(), sshPublicKey ? OpenPgpTagType.PublicKey : OpenPgpTagType.SignaturePublicKey);
                }
                else
                {
                    await Source.ReadUntilEofAsync().ConfigureAwait(false);
                    return (null, default);
                }
            }
            else if (_isDer)
            {
                if (first)
                {
                    return (new DerBucket(Source.NoDispose()), OpenPgpTagType.DerValue);
                }
                else
                {
                    await Source.ReadUntilEofAsync().ConfigureAwait(false);
                    return (null, default);
                }
            }
            else
            {
                byte? bq = await inner.ReadByteAsync().ConfigureAwait(false);

                if (bq is null)
                    return (null, default);

                byte b = bq.Value;
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
                    var r = await ReadLengthAsync(inner).ConfigureAwait(false);

                    if (r.PartialResult)
                    {
                        _reading = true;
                        return (new OpenPgpPartialBodyBucket(inner.NoDispose(), r.Length!.Value).AtEof(() => _reading = false), tag);
                    }

                    remaining = r.Length ?? throw new BucketEofException(Source);
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
                        0 => await inner.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(Source),
                        1 => await inner.ReadNetworkUInt16Async().ConfigureAwait(false),
                        2 => await inner.ReadNetworkUInt32Async().ConfigureAwait(false),
                        _ => throw new NotImplementedException("Indetermined size"),
                    };
                }

                _reading = true;
                return (inner.NoDispose().TakeExactly(remaining).AtEof(() => _reading = false), tag);
            }
        }

        internal static async ValueTask<(uint? Length, bool PartialResult)> ReadLengthAsync(Bucket bucket)
        {
            byte? b = await bucket.ReadByteAsync().ConfigureAwait(false);

            if (!b.HasValue)
                return (null, false);

            if (b < 192)
                return (b, false);

            else if (b < 224)
            {
                byte b2 = await bucket.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(bucket);

                return ((uint)((b - 192 << 8) + b2 + 192), false);
            }
            else if (b == 255)
            {
                return (await bucket.ReadNetworkUInt32Async().ConfigureAwait(false), false);
            }
            else
            {
                uint? partialBodyLen = 1u << (b & 0x1F);

                return (partialBodyLen, true);
            }
        }
    }
}


