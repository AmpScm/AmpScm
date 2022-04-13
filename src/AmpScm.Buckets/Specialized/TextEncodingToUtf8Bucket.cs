using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    internal class TextEncodingToUtf8Bucket : WrappingBucket
    {
        readonly char[] _charBuffer;
        byte[] _utfBuffer;
        byte[]? _toConvert;
        BucketBytes _remaining;
        Encoding _encoding;
        Decoder _decoder;
        Encoder _encoder;
        bool _by2;
        private int _toEncode;
        long _position;

        public TextEncodingToUtf8Bucket(Bucket inner, Encoding encoding) : base(inner)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));
            _encoding = encoding;
            _decoder = encoding.GetDecoder();
            _encoder = Encoding.UTF8.GetEncoder();
            _charBuffer = new char[1024];
            _utfBuffer = new byte[1024];
            _by2 = encoding is UnicodeEncoding;
        }

        public override string Name => "ToUtf8[" + _encoding.WebName + "]>" + Inner.Name;

        public override long? Position => _position;

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            int rq = _by2 ? (requested + 1) & ~1 : requested;

            while (_remaining.IsEmpty)
            {
                BucketBytes bb;

                if (_toConvert == null)
                {
                    bb = await Inner.ReadAsync(rq).ConfigureAwait(false);

                    if (bb.IsEof)
                        return bb;
                }
                else
                {
                    bb = _toConvert;
                    _toConvert = null;
                }

#if !NETFRAMEWORK
                _decoder.Convert(bb.Span, new Span<char>(_charBuffer, _toEncode, _charBuffer.Length - _toEncode), false, out var bytesUsed, out var charsDecoded, out var completed);

#else
                _decoder.Convert(bb.ToArray(), 0, bb.Length, _charBuffer, _toEncode, _charBuffer.Length-_toEncode, false, out var bytesUsed, out var charsDecoded, out var completed);
#endif
                charsDecoded += _toEncode;
                _toEncode = 0;

                if (bytesUsed < bb.Length)
                    _toConvert = bb.Slice(bytesUsed).ToArray();

                _encoder.Convert(_charBuffer, 0, charsDecoded, _utfBuffer, 0, _utfBuffer.Length, false, out var charsEncoded, out var utfBytesUsed, out var utfCompleted);

                if (charsEncoded < charsDecoded)
                {
                    _toEncode = charsDecoded - charsEncoded;
                    for (int i = 0; i < _toEncode; i++)
                    {
                        _charBuffer[i] = _charBuffer[i + charsDecoded];
                    }
                }
                else
                    _toEncode = 0;

                _remaining = new BucketBytes(_utfBuffer, 0, utfBytesUsed);
            }

            if (!_remaining.IsEmpty)
            {
                requested = Math.Min(requested, _remaining.Length);

                var r = _remaining.Slice(0, requested);
                _remaining = _remaining.Slice(requested);
                _position += requested;
                return r;
            }
            else
                return BucketBytes.Eof;
        }

        public override BucketBytes Peek()
        {
            return _remaining;
        }

        public override bool CanReset => Inner.CanReset;

        public override async ValueTask ResetAsync()
        {
            await Inner.ResetAsync().ConfigureAwait(false);

            _position = 0;
            _remaining = BucketBytes.Empty;
        }
    }
}
