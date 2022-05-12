using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Client.Buckets
{
    internal sealed class HttpChunkBucket : WrappingBucket
    {
        BucketBytes _remaining;
        BucketBytes _next;
        bool _addEol;
        bool _eof;

        static ReadOnlyMemory<byte> CRLF = new byte[] { 0x0d, 0x0a };
        static ReadOnlyMemory<byte> ZeroCRLFCRLF = new byte[] { (byte)'0', 0x0d, 0x0a, 0x0d, 0x0a };

        public HttpChunkBucket(Bucket inner) : base(inner)
        {
        }

        public override BucketBytes Peek()
        {
            return _remaining;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (_remaining.IsEmpty)
            {
                if (!_next.IsEmpty)
                {
                    _remaining = _next;
                    _next = BucketBytes.Empty;
                }
                else if (_addEol)
                {
                    _remaining = CRLF;
                    _addEol = false;
                }
                else if (!_eof)
                {
                    // TODO: Perhaps use remaining bytes, and...
                    _next = await Inner.ReadAsync(int.MaxValue).ConfigureAwait(false);

                    if (_next.IsEof || _next.IsEmpty)
                    {
                        _eof = true;
                        _remaining = ZeroCRLFCRLF;
                    }
                    else
                    {
                        _remaining = Encoding.ASCII.GetBytes($"{Convert.ToString(_next.Length, 16)}\r\n");
                        _addEol = true;
                    }
                }
            }

            if (!_remaining.IsEmpty)
            {
                var r = _remaining.Slice(0, Math.Min(requested, _remaining.Length));
                _remaining = _remaining.Slice(r.Length);

                if (_remaining.IsEmpty && !_next.IsEmpty)
                {
                    _remaining = _next;
                    _next = BucketBytes.Empty;
                }
                return r;
            }

            return BucketBytes.Eof;
        }
    }
}
