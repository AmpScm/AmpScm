using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    public class TextEncodingToUtf8Bucket : WrappingBucket
    {
        readonly char[] _charBuffer;
        byte[] _utfBuffer;
        byte[]? _toConvert;
        BucketBytes _remaining;
        Encoding _encoding;
        bool _by2;

        public TextEncodingToUtf8Bucket(Bucket inner, Encoding encoding) : base(inner)
        {
            _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            _charBuffer = new char[1024];
            _utfBuffer = new byte[1024];
            _by2 = _encoding is UnicodeEncoding;
        }

        static Encoding UTF8 => Encoding.UTF8;

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            int rq = _by2 ? (requested + 1) & ~1 : requested;

            while(_remaining.IsEmpty)
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

                int chars;
#if !NETFRAMEWORK
                chars = _encoding.GetCharCount(bb.Span);
                int converted = _encoding.GetChars(bb.Span, _charBuffer);
#else
                var bytes = bb.ToArray();
                chars = _encoding.GetCharCount(bytes);
                int converted = _encoding.GetChars(bytes, 0, bb.Length, _charBuffer, 0);
#endif

                //if (converted < bb.Length)
                //    _toConvert = bb.Slice(converted).ToArray();

                _remaining = UTF8.GetBytes(_charBuffer, 0, converted);
            }

            if (!_remaining.IsEmpty)
            {
                requested = Math.Min(requested, _remaining.Length);

                var r = _remaining.Slice(0, requested);
                _remaining = _remaining.Slice(requested);
                return r;
            }
            else
                return BucketBytes.Eof;
        }

        public override BucketBytes Peek()
        {
            return _remaining;
        }
    }
}
