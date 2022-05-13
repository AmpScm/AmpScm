using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    internal class CompressionBucket : WrappingBucket, IBucketNoClose
    {
        private protected Stream Src { get; }
        protected Stream Processed { get; }
        byte[]? buffer;
        bool _eof;
        bool _writeCompression;
        AggregateBucket? _written;
        BucketBytes _remaining;

        public CompressionBucket(Bucket inner, Func<Stream, Stream> compressor) : base(inner.NoClose())
        {
            Src = Inner.AsStream(new Writer(this));
            Processed = compressor(Src);

            _writeCompression = !Processed.CanRead && Processed.CanWrite;
            if (_writeCompression)
                _written = new AggregateBucket.Simple();
        }

        protected override void InnerDispose()
        {
            try
            {
                Processed.Close();
            }
            finally
            {
                base.InnerDispose();
            }
        }

        public override BucketBytes Peek()
        {
            return _remaining;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (!_remaining.IsEmpty)
            {
                var bb = _remaining.Slice(0, Math.Min(requested, _remaining.Length));
                _remaining = _remaining.Slice(bb.Length);
                return bb;
            }

            await Refill().ConfigureAwait(false);

            if (!_remaining.IsEmpty)
            {
                var bb = _remaining.Slice(0, Math.Min(requested, _remaining.Length));
                _remaining = _remaining.Slice(bb.Length);
                return bb;
            }

            return BucketBytes.Eof;
        }

        async ValueTask Refill()
        {
            if (!_writeCompression)
            {
                if (buffer == null)
                    buffer = new byte[4096];

#if !NETFRAMEWORK
                int nRead = await Processed.ReadAsync(buffer).ConfigureAwait(false);
#else
                int nRead = await Processed.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
#endif

                if (nRead > 0)
                {
                    _remaining = new BucketBytes(buffer, 0, nRead);
                }
                else
                {
                    _remaining = BucketBytes.Eof;
                }
            }
            else
            {
                _remaining = await _written!.ReadAsync().ConfigureAwait(false);

                while (_remaining.IsEmpty)
                {
                    var bb = await Inner.ReadAsync().ConfigureAwait(false);

                    if (bb.IsEof)
                    {
                        if (!_eof)
                        {
                            Processed.Close(); // Flush
                            _eof = true;
                        }
                        else
                            return;
                    }
                    else
                    {
#if !NETFRAMEWORK
                        await Processed.WriteAsync(bb.Memory).ConfigureAwait(false);
#else
                        var (bytes, offset) = bb.ExpandToArray();
                        await Processed.WriteAsync(bytes, offset, bytes.Length).ConfigureAwait(false);
#endif
                    }

                    _remaining = await _written!.ReadAsync().ConfigureAwait(false);
                }
            }
        }

        Bucket IBucketNoClose.NoClose()
        {
            base.NoClose();
            return this;
        }

        bool IBucketNoClose.HasMoreClosers()
        {
            return base.HasMoreClosers();
        }

        private class Writer : IBucketWriter
        {
            CompressionBucket Bucket { get; }

            public Writer(CompressionBucket bucket)
            {
                Bucket = bucket;
            }
            public ValueTask ShutdownAsync()
            {
                return default;
            }

            public void Write(Bucket bucket)
            {
                Bucket._written!.Append(bucket);
            }
        }
    }
}
