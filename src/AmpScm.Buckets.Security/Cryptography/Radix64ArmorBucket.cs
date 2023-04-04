using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Cryptography
{
    public sealed class Radix64ArmorBucket : WrappingBucket, IBucketPoll
    {
        private SState _state;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private Bucket? _base64Decode;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private int? _crc24Result;
        private bool _sshBegin;

        internal ReadOnlyMemory<byte>? PublicKeyType { get; private set; }

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
                else if (bb.Slice(eol).EndsWithASCII(" PUBLIC KEY-----"))
                {
                    PublicKeyType = bb.Slice(sl, bb.Length - sl - 15 - eol.CharCount()).ToArray();
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
                _base64Decode = new StopAtLineStartBucket(bb.Memory.AsBucket() + Source.NoDispose(), new byte[] { (byte)'-' }).Base64Decode(true);
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

                _base64Decode.Dispose();
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
            return new StopAtLineStartBucket(Source.NoDispose(), new byte[] { (byte)'=', (byte)'-' }).Base64Decode(true).Crc24(x => _crc24Result = x);
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

            public StopAtLineStartBucket(Bucket source, params byte[] stopAt) : base(source)
            {
                _stopAt = (byte[])stopAt.Clone();
            }

            public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
            {
                if (_eof)
                    return BucketBytes.Eof;

                var bb = Source.Peek();

                if (bb.Length > 0 && _stopAt.Contains(bb[0]))
                {
                    _eof = true;
                    return BucketBytes.Eof;
                }

                (bb, _) = await Source.ReadExactlyUntilEolAsync(BucketEol.LF, requested: requested).ConfigureAwait(false);
                return bb;
            }

            public override BucketBytes Peek()
            {
                var bb = Source.Peek();

                if (bb.Length > 0 && _stopAt.Contains(bb[0]))
                {
                    return BucketBytes.Eof;
                }

                int n = bb.IndexOf((byte)'\n');

                if (n > 0)
                    return bb.Slice(0, n + 1);
                else
                    return bb;
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
}
