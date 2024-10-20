﻿using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Cryptography;

public sealed class Radix64ArmorBucket : WrappingBucket, IBucketPoll
{
    private SState _state;
#pragma warning disable CA2213 // Disposable fields should be disposed
    private Bucket? _base64Decode;
#pragma warning restore CA2213 // Disposable fields should be disposed
    private int? _crc24Result;
    private bool _sshBegin;

    internal string? PublicKeyType { get; private set; }

    private enum SState
    {
        Init,
        Headers,
        Body,
        Crc,
        Trailer,
        Eof
    }
    public Radix64ArmorBucket(Bucket source) : base(source)
    {
    }

    public async ValueTask<BucketBytes> ReadHeaderAsync()
    {
        if (_state >= SState.Body)
            return BucketBytes.Eof;

        var (bb, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.CRLF | BucketEol.LF).ConfigureAwait(false);

        if (_state == SState.Init)
        {
            bb = bb.TrimStart().TrimEnd(eol);

            if (!bb.StartsWithASCII("-----BEGIN "))
            {
                if (!bb.StartsWithASCII("---- BEGIN "))
                    throw new BucketException("Expected '-----BEGIN '");

                _sshBegin = true;
            }
            int sl = "-----BEGIN ".Length;

            if (bb.Slice(sl).StartsWithASCII("SSH "))
            {
                _base64Decode = SetupDecode();
                _state = SState.Body;
                return BucketBytes.Eof;
            }
            else if (bb.EndsWithASCII(" PUBLIC KEY-----"))
            {
                PublicKeyType = bb.Slice(sl, bb.Length - sl - 15).Trim().ToUTF8String();
            }
            else if (bb.EndsWithASCII(" SignaturePublicKey-----"))
            {
                // PGP key
            }

            _state = SState.Headers;

            (bb, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.CRLF | BucketEol.LF).ConfigureAwait(false);
        }

        if (bb.IsEmpty || bb.TrimEnd(eol).IsEmpty)
        {
            _base64Decode = SetupDecode();
            _state = SState.Body;
            return BucketBytes.Eof;
        }
        else if (0 > bb.IndexOf((byte)':'))
        {
            _base64Decode = new StopAtLineStartBucket(bb.AsBucket() + Source.NoDispose(), "-"u8.ToArray()).Base64Decode(lineMode: true);
            _state = SState.Body;
            return BucketBytes.Eof;
        }
        else
            return bb.Slice(eol);
    }

    public override BucketBytes Peek()
    {
        if (_state != SState.Body)
            return BucketBytes.Empty;

        return _base64Decode?.Peek() ?? BucketBytes.Empty;
    }

    public async ValueTask<BucketBytes> PollAsync(int minRequested = 1)
    {
        while (_state < SState.Body)
        {
            await ReadHeaderAsync().ConfigureAwait(false);
        }

        if (_state != SState.Body)
            return BucketBytes.Empty;

        if (_base64Decode is null)
            return BucketBytes.Empty;

        return await _base64Decode.PollAsync(minRequested).ConfigureAwait(false);
    }


    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        if (_state == SState.Eof)
            return BucketBytes.Eof;

        while (_state < SState.Body)
        {
            await ReadHeaderAsync().ConfigureAwait(false);
        }

        if (_state == SState.Body)
        {
            _base64Decode ??= SetupDecode();

            var bb = await _base64Decode.ReadAsync(requested).ConfigureAwait(false);

            if (!bb.IsEof)
                return bb;

            await _base64Decode.DisposeAsync();
            _base64Decode = null;
            _state = SState.Crc;
        }

        while (_state > SState.Body && _state < SState.Eof)
        {
            var (bb, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.CRLF | BucketEol.LF).ConfigureAwait(false);

            if (bb.IsEof)
                throw new BucketEofException(Source);

            if (_state == SState.Crc && bb.TrimStart().StartsWithASCII("="))
            {
                string crc = bb.Trim(eol).ToASCIIString(1);

                byte[] crcData = Convert.FromBase64String(crc);
                byte[] b4 = new byte[4];
                crcData.CopyTo(b4, 1);

                if (_crc24Result != NetBitConverter.ToInt32(b4, 0))
                {
                    throw new BucketException($"CRC mismatch in signature from {Source.Name} Bucket");
                }

                _state = SState.Trailer;
            }
            else if (bb.TrimStart().StartsWithASCII(_sshBegin ? "---- END " : "-----END "))
            {
                _state = SState.Eof;
                return BucketBytes.Eof;
            }
        }

        return BucketBytes.Eof;
    }

    private Bucket SetupDecode()
    {
        return new StopAtLineStartBucket(Source.NoDispose(), "=-"u8.ToArray()).Base64Decode(lineMode: true).Crc24(x => _crc24Result = x);
    }

    public static bool IsHeader(BucketBytes bb, BucketEol eol)
    {
        if (!bb.StartsWithASCII("-----BEGIN ") || !bb.Slice(eol).EndsWithASCII("-----"))
            return false;

        bb = bb.Slice(11, bb.Length - 11 - 5 - eol.CharCount());

        if (!bb.EndsWithASCII(" SIGNATURE")
            && !bb.EndsWithASCII(" MESSAGE"))
        {
            return false;
        }

        return true;
    }

    private sealed class StopAtLineStartBucket : WrappingBucket
    {
        private readonly byte[] _stopAt;
        private bool _eof;
        private BucketBytes _line;

        public StopAtLineStartBucket(Bucket source, params byte[] stopAt) : base(source)
        {
            _stopAt = (byte[])stopAt.Clone();
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            if (!_line.IsEmpty)
            {
                var r = _line.Slice(0, requested);
                _line = _line.Slice(r.Length);
                return r;
            }

            if (_eof)
                return BucketBytes.Eof;

            var bb = await Source.PollAsync(1).ConfigureAwait(false);

            for (int i = 0; i < bb.Length; i++)
            {
                if (_stopAt.Contains(bb[i]))
                {
                    _eof = true;
                    return BucketBytes.Eof;
                }
                else if (bb[i] == ' ' || bb[i] == '\t')
                    continue;
                else
                    break;
            }

            if (bb.Length > 0 && _stopAt.Contains(bb[0]))
            {
                _eof = true;
                return BucketBytes.Eof;
            }

            (_line, _) = await Source.ReadExactlyUntilEolAsync(BucketEol.LF).ConfigureAwait(false);

            bb = _line.Slice(0, Math.Min(requested, _line.Length));
            _line = _line.Slice(bb.Length);

            return bb;
        }

        public override BucketBytes Peek()
        {
            return _line;
        }
    }

    public override bool CanReset => Source.CanReset;

    public override void Reset()
    {
        base.Reset();

        _state = default;
        _base64Decode = null;
        _crc24Result = default;
        _sshBegin = false;
        PublicKeyType = null;
    }
}
