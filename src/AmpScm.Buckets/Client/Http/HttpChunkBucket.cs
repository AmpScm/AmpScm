using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Client.Http
{
    internal sealed class HttpChunkBucket : WrappingBucket
    {
        BucketBytes _remaining;
        BucketBytes _next;
        bool _eof;
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
                else if (!_eof)
                {
                    _next = await Inner.ReadAsync(int.MaxValue).ConfigureAwait(false);
                    string size;

                    if (_next.IsEof || _next.IsEmpty)
                    {
                        _eof = true;
                        size = "0\r\n\r\n";
                    }
                    else
                        size = $"{Convert.ToString(_next.Length, 16)}\r\n";

                    _remaining = Encoding.ASCII.GetBytes(size);

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
