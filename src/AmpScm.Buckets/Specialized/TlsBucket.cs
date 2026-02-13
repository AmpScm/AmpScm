using System.Diagnostics;
using System.Net.Security;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized;

public sealed class TlsBucket : WrappingBucket, IBucketWriter, IBucketWriterStats, IBucketPoll
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly byte[] _inputBuffer;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private BucketBytes _unread;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
#pragma warning disable CA2213 // Disposable fields should be disposed
    private readonly SslStream _stream;
#pragma warning restore CA2213 // Disposable fields should be disposed
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _writeEof;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _readEof;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Task? _writing;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _authenticated;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly string _targetHost;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private long _bytesRead;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IBucketWriter InnerWriter { get; }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int BufferSize { get; }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private WaitForDataBucket WriteBucket { get; } = new WaitForDataBucket();

    public TlsBucket(Bucket source, IBucketWriter writer, string targetHost, int bufferSize = 16384)
        : base(source)
    {
        InnerWriter = writer;
        BufferSize = bufferSize;
        _inputBuffer = new byte[BufferSize];
        _stream = new SslStream(Source.AsStream(InnerWriter));
        _targetHost = targetHost;
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        try
        {
            _stream.Dispose();
        }
        finally
        {
            await base.DisposeAsync(disposing).ConfigureAwait(false);
        }
    }

    public async ValueTask ShutdownAsync()
    {
        if (_authenticated)
            await _stream.ShutdownAsync().ConfigureAwait(false);
    }

    public override BucketBytes Peek()
    {
        return _unread;
    }

    async ValueTask<BucketBytes> IBucketPoll.PollAsync(int minRequested)
    {
        if (_unread.IsEmpty && !_readEof && _authenticated)
        {
            // Refill the buffer
            await ReadFromStream().ConfigureAwait(false);
        }

        return Peek();
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        if (requested < 0)
            throw new ArgumentOutOfRangeException(nameof(requested), requested, "Must be positive");
        if (!_authenticated)
        {
            await _stream.AuthenticateAsClientAsync(_targetHost).ConfigureAwait(false);

            _authenticated = true;
        }

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

        return await reading.ConfigureAwait(false);
    }

    private async Task<BucketBytes> DoRead(int requested)
    {
        if (_unread.Length == 0 && !_readEof)
            await ReadFromStream().ConfigureAwait(false);

        if (_unread.Length > 0)
        {
            var bb = _unread.Slice(0, Math.Min(requested, _unread.Length));
            _unread = _unread.Slice(bb.Length);
            return bb;
        }
        else
        {
            _readEof = true;
            return BucketBytes.Eof;
        }
    }

    private async ValueTask ReadFromStream()
    {
        int length;
#if NETFRAMEWORK
        length = await _stream.ReadAsync(_inputBuffer, 0, _inputBuffer.Length).ConfigureAwait(false);
#else
        length = await _stream.ReadAsync(new Memory<byte>(_inputBuffer)).ConfigureAwait(false);
#endif
        _bytesRead += length;
        _unread = new BucketBytes(_inputBuffer, 0, length);
    }

    public override long? Position => _bytesRead - _unread.Length;

    public long BytesWritten { get; private set; }

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
                }
            }

            if (bb.Length > 0)
            {
#if NETFRAMEWORK
                var (arr, offs) = bb.ExpandToArray();

                await _stream.WriteAsync(arr!, offs, bb.Length).ConfigureAwait(false);
#else
                await _stream.WriteAsync(bb.Memory).ConfigureAwait(false);
#endif
                BytesWritten += bb.Length;
            }
        }
    }

    public void Write(Bucket bucket)
    {
        WriteBucket.Write(bucket);
    }

    public override bool CanReset => false;

    public override string Name => $"TLS[{_stream.SslProtocol}]>{Source.Name}";
}
