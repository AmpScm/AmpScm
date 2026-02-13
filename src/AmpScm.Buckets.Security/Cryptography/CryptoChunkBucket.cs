using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Cryptography;

internal sealed class CryptoChunkBucket : WrappingBucket
{
    private bool _notFirst;
    private bool _isSsh;
    private bool _reading;
    private bool _isDer;

    public CryptoChunkBucket(Bucket source) : base(source)
    {
        Source = base.Source;
    }

    public bool IsSsh => _isSsh;

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        while (true)
        {
            var (bucket, _) = await ReadChunkAsync().ConfigureAwait(false);

            if (bucket is null)
                return BucketBytes.Eof;
        }
    }

    public override BucketBytes Peek()
    {
        return BucketBytes.Empty;
    }

    public new Bucket Source { get; private set; }

    public async ValueTask<(Bucket? Bucket, CryptoTag Type)> ReadChunkAsync()
    {
        if (_reading)
            throw new BucketException("Can't obtain new packet, when the last one is not completely read");

        bool first = false;
        bool sshPublicKey = false;
        if (!_notFirst)
        {
            bool didRead = false;
            var bb = await Source.PollAsync().ConfigureAwait(false);

            if (bb.Length < 6)
            {
                bb = await Source.ReadAtLeastAsync(6, throwOnEndOfStream: false).ConfigureAwait(false);
                didRead = true;
            }

            if (bb.TrimStart().StartsWith("---"u8))
            {
                Source = new Radix64ArmorBucket(didRead ? bb.AsBucket() + Source : Source);

                bb = await Source.ReadAtLeastAsync(6, throwOnEndOfStream: false).ConfigureAwait(false);
                didRead = true;
            }

            if (bb.StartsWith("SSHSIG"u8))
            {
                _isSsh = true;
                if (!didRead)
                {
                    // Make sure we skip the "SSHSIG"
                    bb = await Source.ReadAtLeastAsync(6, throwOnEndOfStream: false).ConfigureAwait(false);
                }
                didRead = false;
            }
            else if (bb.Span.StartsWith(new byte[] { 0x00, 0x00, 0x00 }))
            {
                _isSsh = true;
                sshPublicKey = true;
            }
            else if (await DerBucket.BytesMayBeDerAsync(bb).ConfigureAwait(false))
            {
                _isDer = true;
            }
            
            if (didRead)
            {
                Source = bb.AsBucket() + Source;
            }
            _notFirst = true;
            first = true;
        }

        if (_isSsh)
        {
            if (first)
            {
                return (Source.NoDispose(), sshPublicKey ? CryptoTag.SshPublicKey : CryptoTag.SshSignaturePublicKey);
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
                return (new DerBucket(Source.NoDispose()), CryptoTag.DerValue);
            }
            else
            {
                await Source.ReadUntilEofAsync().ConfigureAwait(false);
                return (null, default);
            }
        }
        else
        {
            byte? bq = await Source.ReadByteAsync().ConfigureAwait(false);

            if (bq is null)
                return (null, default);

            byte b = bq.Value;
            bool oldFormat;
            CryptoTag tag;
            uint remaining = 0;

            if ((b & 0x80) == 0)
                throw new BucketException("Bad packet");

            oldFormat = 0 == (b & 0x40);
            if (oldFormat)
            {
                tag = (CryptoTag)((b & 0x3c) >> 2);
                remaining = (uint)(b & 0x3);
            }
            else
                tag = (CryptoTag)(b & 0x3F);

            Console.WriteLine(tag);

            if (!oldFormat)
            {
                var r = await ReadLengthAsync(Source).ConfigureAwait(false);

                if (r.PartialResult)
                {
                    _reading = true;
                    return (new PgpPartialBodyBucket(Source.NoDispose(), r.Length!.Value).AtEof(() => _reading = false), tag);
                }

                remaining = r.Length ?? throw new BucketEofException(Source);
            }
            else if (remaining == 3)
            {
                // Indetermined size, upto end
                return (Source.NoDispose().AtEof(() => _reading = false), tag);
            }
            else
            {
                remaining = remaining switch
                {
                    0 => await Source.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(Source),
                    1 => await Source.ReadNetworkUInt16Async().ConfigureAwait(false),
                    2 => await Source.ReadNetworkUInt32Async().ConfigureAwait(false),
                    _ when (await Source.ReadRemainingBytesAsync().ConfigureAwait(false)) is { } size => checked((uint)size), // Old definition: until end of stream
                    _ => throw new NotSupportedException("Indetermined size"),
                };
            }

            _reading = true;
            return (Source.NoDispose().TakeExactly(remaining).AtEof(() => _reading = false), tag);
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

    public override bool CanReset => Source.CanReset;

    public override void Reset()
    {
        base.Reset();

        _notFirst = false;
        _isSsh = false;
        _reading = false;
        _isDer = false;
    }
}


