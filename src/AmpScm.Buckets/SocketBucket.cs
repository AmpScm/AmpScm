using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets;

public class SocketBucket : Bucket, IBucketWriter, IBucketWriterStats
{
    private readonly byte[] _inputBuffer;
    private BucketBytes _unread;
    private bool _readEof, _writeEof;
    private Task? _writing;
    private long _bytesRead;
    private protected Socket Socket { get; }

    internal WaitForDataBucket WriteBucket { get; } = new WaitForDataBucket();

    public SocketBucket(Socket socket, int bufferSize = 16384)
    {
        Socket = socket ?? throw new ArgumentNullException(nameof(socket));
        _inputBuffer = new byte[bufferSize];
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
                Socket.Dispose();
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public async ValueTask ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
#if !NET6_0_OR_GREATER
        await Socket.ConnectAsync(host, port).ConfigureAwait(false);

#else
        await Socket.ConnectAsync(host, port, cancellationToken: cancellationToken).ConfigureAwait(false);
#endif
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        Task<BucketBytes> reading = DoRead(requested);
        Task ready;
        do
        {
            if (_writeEof)
                break; // Use wait at return for reading

            _writing ??= HandleWriting();

            ready = await Task.WhenAny(reading, _writing).ConfigureAwait(false);

            if (ready == _writing)
                _writing = null;
        }
        while (ready != reading);

        // Task already don, but just do it properly
        return await reading.ConfigureAwait(false);
    }

    public override BucketBytes Peek()
    {
        return _unread;
    }

    private async Task<BucketBytes> DoRead(int requested)
    {
        if (_unread.Length > 0)
        {
            var bb = _unread.Slice(0, Math.Min(requested, _unread.Length));
            _unread = _unread.Slice(bb.Length);
            return bb;
        }
        else if (_readEof)
            return BucketBytes.Eof;

        int len = await Socket.ReceiveAsync(new ArraySegment<byte>(_inputBuffer), SocketFlags.None).ConfigureAwait(false);

        if (len > 0)
        {
            _bytesRead += len;
            if (len > requested)
            {
                _unread = new BucketBytes(_inputBuffer, requested, len - requested);
                return new BucketBytes(_inputBuffer, 0, requested);
            }
            else
            {
                return new BucketBytes(_inputBuffer, 0, len);
            }
        }
        else
        {
            _readEof = true;
            return BucketBytes.Eof;
        }
    }

    public override long? Position => _bytesRead - _unread.Length;

#pragma warning disable CA1033 // Interface methods should be callable by child types
    long IBucketWriterStats.BytesWritten => BytesWritten;
#pragma warning restore CA1033 // Interface methods should be callable by child types
    internal long BytesWritten { get; private set; }

    private async Task HandleWriting()
    {
        while (true)
        {
            var bb = await WriteBucket.ReadAsync().ConfigureAwait(false);

            if (bb.IsEof)
            {
                if (!_writeEof)
                {
                    _writeEof = true;
                    Socket.Shutdown(SocketShutdown.Send);
                }
            }

            while (bb.Length > 0)
            {
#if NET6_0_OR_GREATER
                int written = await Socket.SendAsync(bb.Memory, SocketFlags.None).ConfigureAwait(false);
#else
                var (arr, offs) = bb.ExpandToArray();

                int written = await Socket.SendAsync(new ArraySegment<byte>(arr!, offs, bb.Length), SocketFlags.None).ConfigureAwait(false);
#endif

                if (written > 0)
                {
                    BytesWritten += written;
                    bb = bb.Slice(written);
                }
                else
                    return;
            }
        }
    }

    public void Write(Bucket bucket)
    {
        WriteBucket.Write(bucket);
    }

    public ValueTask ShutdownAsync()
    {
        return WriteBucket.ShutdownAsync();
    }
}
