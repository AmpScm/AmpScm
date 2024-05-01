using System.Buffers;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized;

internal sealed class CompressionBucket : WrappingBucket
{
    private Stream Src { get; }
    private Stream Processed { get; }

    private byte[]? _buffer;
    private bool _eof;
    private readonly bool _writeCompression;
#pragma warning disable CA2213 // Disposable fields should be disposed
    private readonly AggregateBucket.SimpleAggregate? _written;
#pragma warning restore CA2213 // Disposable fields should be disposed
    private BucketBytes _remaining;

    public CompressionBucket(Bucket source, Func<Stream, Stream> compressor) : base(source.NoDispose())
    {
        Src = Source.AsStream(new Writer(this));
        Processed = compressor(Src);

        _writeCompression = !Processed.CanRead && Processed.CanWrite;
        if (_writeCompression)
            _written = new AggregateBucket.SimpleAggregate();
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                if (_buffer != null)
                {
                    ArrayPool<Byte>.Shared.Return(_buffer);
                    _buffer = null;
                }
                Processed.Close();
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public override BucketBytes Peek()
    {
        return _remaining;
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
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

    private async ValueTask Refill()
    {
        if (!_writeCompression)
        {
            if (_buffer == null)
                _buffer = ArrayPool<Byte>.Shared.Rent(4096);

#if !NETFRAMEWORK
            int nRead = await Processed.ReadAsync(_buffer).ConfigureAwait(false);
#else
            int nRead = await Processed.ReadAsync(_buffer, 0, _buffer.Length).ConfigureAwait(false);
#endif

            if (nRead > 0)
            {
                _remaining = new BucketBytes(_buffer, 0, nRead);
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
                var bb = await Source.ReadAsync().ConfigureAwait(false);

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

    private sealed class Writer : IBucketWriter
    {
        private CompressionBucket Bucket { get; }

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
