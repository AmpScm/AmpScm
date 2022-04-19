using System;
using System.IO;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Wrappers
{
    internal class StreamBucket : Bucket
    {
        readonly Stream _stream;
        readonly byte[] _buffer;
        long? _initialPosition;
        BucketBytes _remaining;


        public StreamBucket(Stream stream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            _stream = stream;

            if (_stream.CanSeek)
            {
                try
                {
                    _initialPosition = _stream.Position;
                }
                catch (NotSupportedException)
                { }
                catch (IOException)
                { }
            }

            _buffer = new byte[8192];
        }

        public override BucketBytes Peek()
        {
            return _remaining;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                    _stream.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (_remaining.Length == 0)
            {
#if !NETFRAMEWORK
                int n = await _stream.ReadAsync(_buffer).ConfigureAwait(false);
#else
                int n = await _stream.ReadAsync(_buffer, 0, _buffer.Length).ConfigureAwait(false);
#endif

                _remaining = new BucketBytes(_buffer, 0, n);
            }

            if (_remaining.Length > 0)
            {
                var r = _remaining.Slice(0, Math.Min(requested, _remaining.Length));
                _remaining = _remaining.Slice(r.Length);
                return r;
            }
            else
                return BucketBytes.Eof;
        }

        public override long? Position
        {
            get
            {
                if (_initialPosition.HasValue)
                    return _stream.Position - _initialPosition.Value;
                else
                    return null;
            }
        }

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            if (_initialPosition == null)
                return default;

            try
            {
                return new ValueTask<long?>(_stream.Length - _stream.Position);
            }
            catch (NotSupportedException)
            { }
            catch (IOException)
            { }

            return default;
        }
    }
}
