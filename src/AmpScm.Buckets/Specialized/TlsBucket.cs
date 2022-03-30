using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    public class TlsBucket : WrappingBucket, IBucketWriter, IBucketWriterStats
    {
        readonly byte[] _inputBuffer;
        BucketBytes _unread;
        readonly SslStream _stream;
        bool _writeEof, _readEof;
        Task? _writing;
        bool _authenticated;
        readonly string _targetHost;
        long _bytesRead;

        IBucketWriter InnerWriter { get; }
        int BufferSize { get; }
        WaitForDataBucket WriteBucket { get; } = new WaitForDataBucket();

        public TlsBucket(Bucket reader, IBucketWriter writer, string targetHost, int bufferSize = 16384)
            : base(reader)
        {
            InnerWriter = writer;
            BufferSize = bufferSize;
            _inputBuffer = new byte[BufferSize];
            _stream = new SslStream(Inner.AsStream(InnerWriter));
            _targetHost = targetHost;
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

        public async ValueTask ShutdownAsync()
        {
            if (_authenticated)
                await _stream.ShutdownAsync().ConfigureAwait(false);
        }

        public override BucketBytes Peek()
        {
            return _unread;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
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

        async Task<BucketBytes> DoRead(int requested)
        {
            if (_unread.Length > 0)
            {
                var bb = _unread.Slice(0, Math.Min(requested, _unread.Length));
                _unread = _unread.Slice(bb.Length);
                return bb;
            }
            else if (_readEof)
                return BucketBytes.Eof;

#if NETFRAMEWORK
            int len = await _stream.ReadAsync(_inputBuffer, 0, _inputBuffer.Length).ConfigureAwait(false);
#else
            int len = await _stream.ReadAsync(new Memory<byte>(_inputBuffer)).ConfigureAwait(false);
#endif

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

        public long BytesWritten { get; private set; }

        async Task HandleWriting()
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

        public override string Name => $"TLS[{_stream.SslProtocol}]>{Inner.Name}";
    }
}
