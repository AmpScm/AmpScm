using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class TextRecoderBucket : WrappingBucket
    {
        private readonly char[] _charBuffer;
        private readonly byte[] _utfBuffer;
        private byte[]? _toConvert;
        private BucketBytes _remaining;
        private readonly Encoding _sourceEncoding;
        private readonly Encoding _targetEncoding;
        private readonly Decoder _decoder;
        private readonly Encoder _encoder;
        private readonly bool _by2;
        private int _toEncode;
        private long _position;
        private bool _preampbleScanned;

        public TextRecoderBucket(Bucket source, Encoding fromEncoding, Encoding? toEncoding = null) : base(source)
        {
            if (fromEncoding is null)
                throw new ArgumentNullException(nameof(fromEncoding));
            _sourceEncoding = fromEncoding;
            _targetEncoding = toEncoding ?? Encoding.UTF8;
            _decoder = fromEncoding.GetDecoder();
            _encoder = _targetEncoding.GetEncoder();
            _charBuffer = new char[1024];
            _utfBuffer = new byte[1024];
            _by2 = fromEncoding is UnicodeEncoding;
        }

        public override string Name => "ToUtf8[" + _sourceEncoding.WebName + "]>" + Source.Name;

        public override long? Position => _position;

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            int rq = _by2 ? (requested + 1) & ~1 : requested;

            while (_remaining.IsEmpty)
            {
                BucketBytes bb;

                if (_toConvert == null)
                {
                    bb = await Source.ReadAsync(rq).ConfigureAwait(false);

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
                                byte? b = await Source.ReadByteAsync().ConfigureAwait(false);

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
                _decoder.Convert(bb.Span, new Span<char>(_charBuffer, _toEncode, _charBuffer.Length - _toEncode), false, out int bytesUsed, out int charsDecoded, out bool completed);

#else
                var (arr, offs) = bb.ExpandToArray();
                _decoder.Convert(arr, offs, bb.Length, _charBuffer, _toEncode, _charBuffer.Length - _toEncode, false, out var bytesUsed, out var charsDecoded, out var completed);
#endif
                charsDecoded += _toEncode;
                _toEncode = 0;

                if (bytesUsed < bb.Length)
                    _toConvert = bb.Slice(bytesUsed).ToArray();

                _encoder.Convert(_charBuffer, 0, charsDecoded, _utfBuffer, 0, _utfBuffer.Length, false, out int charsEncoded, out int utfBytesUsed, out bool utfCompleted);

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

        public override bool CanReset => Source.CanReset;

        public override void Reset()
        {
            Source.Reset();

            _position = 0;
            _remaining = BucketBytes.Empty;
        }

        public override Bucket Duplicate(bool reset = false)
        {
            var wrapped = Source.Duplicate(reset);

            return new TextRecoderBucket(wrapped, _sourceEncoding, _targetEncoding);
        }
    }
}
