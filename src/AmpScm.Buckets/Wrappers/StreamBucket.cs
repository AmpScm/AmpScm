﻿namespace AmpScm.Buckets.Wrappers;

internal sealed class StreamBucket : Bucket
{
    private readonly Stream _stream;
    private readonly byte[] _buffer;
    private readonly long? _initialPosition;
    private BucketBytes _remaining;


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

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        try
        {
            if (disposing)
                _stream.Dispose();
        }
        finally
        {
            await base.DisposeAsync(disposing);
        }
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
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
            return new(_stream.Length - _stream.Position + _remaining.Length);
        }
        catch (NotSupportedException)
        { }
        catch (IOException)
        { }

        return default;
    }
}
