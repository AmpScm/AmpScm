using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    internal class TextRecoderBucket : WrappingBucket
    {
        readonly char[] _charBuffer;
        byte[] _utfBuffer;
        byte[]? _toConvert;
        BucketBytes _remaining;
        Encoding _sourceEncoding;
        Encoding _targetEncoding;
        Decoder _decoder;
        Encoder _encoder;
        bool _by2;
        private int _toEncode;
        long _position;
        bool _preampbleScanned;

        public TextRecoderBucket(Bucket inner, Encoding fromEncoding, Encoding? toEncoding=null) : base(inner)
        {
            if (fromEncoding == null)
                throw new ArgumentNullException(nameof(fromEncoding));
            _sourceEncoding = fromEncoding;
            _targetEncoding = toEncoding ?? Encoding.UTF8;
            _decoder = fromEncoding.GetDecoder();
            _encoder = _targetEncoding.GetEncoder();
            _charBuffer = new char[1024];
            _utfBuffer = new byte[1024];
            _by2 = fromEncoding is UnicodeEncoding;
        }

        public override string Name => "ToUtf8[" + _sourceEncoding.WebName + "]>" + Inner.Name;

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

                    if (_position == 0 && !_preampbleScanned && _sourceEncoding.GetPreamble() is byte[] preample && preample.Length > 0)
                    {
                        _preampbleScanned = true;

                        int len = Math.Min(preample.Length, bb.Length);
                        if (bb.Span.Slice(0, len).SequenceEqual(preample.AsSpan().Slice(0, len)))
                        {
                            bool isMatch = true;
                            while (len < preample.Length && isMatch)
                            {
                                byte? b = await Inner.ReadByteAsync().ConfigureAwait(false);

                                if (b is null)
                                {
                                    _toConvert = bb.ToArray();
                                    isMatch = false;
                                    break;
                                }

                                bb = bb.ToArray().ArrayAppend(b.Value);
                                len++;

                                if (!bb.Span.Slice(0, len).SequenceEqual(preample.AsSpan().Slice(0, len)))
                                    isMatch = false;
                            }

                            if (isMatch)
                                bb = bb.Slice(preample.Length);
                        }

                        if (bb.Length == 0)
                            continue;
                    }
                }
                else
                {
                    bb = _toConvert;
                    _toConvert = null;
                }

#if !NETFRAMEWORK
                _decoder.Convert(bb.Span, new Span<char>(_charBuffer, _toEncode, _charBuffer.Length - _toEncode), false, out var bytesUsed, out var charsDecoded, out var completed);

#else
                var (arr, offs) = bb.ExpandToArray();
                _decoder.Convert(arr, offs, bb.Length, _charBuffer, _toEncode, _charBuffer.Length-_toEncode, false, out var bytesUsed, out var charsDecoded, out var completed);
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

        public override void Reset()
        {
            Inner.Reset();

            _position = 0;
            _remaining = BucketBytes.Empty;
        }

        public override Bucket Duplicate(bool reset = false)
        {
            var wrapped = Inner.Duplicate(reset);

            return new TextRecoderBucket(wrapped, _sourceEncoding, _targetEncoding);
        }
    }
}
