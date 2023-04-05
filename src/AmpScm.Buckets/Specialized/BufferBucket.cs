using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class BufferBucket : WrappingBucket, IBucketSeek, IBucketPoll
    {
        private readonly int _maxRam;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly AggregateBucket.SimpleAggregate _readBucket;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly /*IBucketWriter*/ MyWriter _writer;
        private long _buffered;
        private long? _size;
        private bool _readEof;
        private bool _disposed;

        public BufferBucket(Bucket source, int maxMemory = 0)
            : base(source)
        {
            if (maxMemory < 0)
                throw new ArgumentOutOfRangeException(nameof(maxMemory));

            _maxRam = maxMemory;
            _readBucket = new AggregateBucket.SimpleAggregate(true, Array.Empty<Bucket>());
            _writer = new MyWriter(this);
        }

        public async override ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            var bb = await _readBucket.ReadAsync(requested).ConfigureAwait(false);
            if (!bb.IsEof || _readEof)
                return bb;

            await BufferSomeMore(requested).ConfigureAwait(false);

            return await _readBucket.ReadAsync(requested).ConfigureAwait(false);
        }

        private async ValueTask BufferSomeMore(int requested)
        {
            if (_readEof)
                return;

            var bb = await Source.ReadAsync(requested).ConfigureAwait(false);

            if (!bb.IsEmpty)
            {
                _buffered += bb.Length;
                _writer.Write(bb.ToArray().AsBucket());
            }
            else
            {
                _size = _buffered;
                _readEof = true;
                Dispose(true);
            }
        }

        public override BucketBytes Peek()
        {
            var bb = _readBucket.Peek();

            if (bb.IsEof && !_readEof)
                return BucketBytes.Empty;
            else
                return bb;
        }

        public async ValueTask<BucketBytes> PollAsync(int minRequested = 1)
        {
            if (!_readEof && Position == _buffered)
            {
                await BufferSomeMore(minRequested).ConfigureAwait(false);
            }

            return await _readBucket.PollAsync(minRequested).ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                base.Dispose(disposing);
            }
        }

        public override long? Position => _readBucket.Position;

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            if (_size is null)
            {
                _size = await Source.ReadRemainingBytesAsync().ConfigureAwait(false);

                if (_size.HasValue)
                    _size += _buffered;
                else
                {
                    do
                    {
                        await BufferSomeMore(Bucket.MaxRead).ConfigureAwait(false);
                    }
                    while (!_size.HasValue && !_readEof);

                    if (!_size.HasValue)
                        _size = -1;
                }
            }

            if (_size < 0)
                return null;

            return _size - Position;
        }

        public override void Reset()
        {
            _readBucket.Reset();
        }

        public async ValueTask SeekAsync(long newPosition)
        {
            while(newPosition > _buffered && !_readEof)
            {
                await BufferSomeMore((int)(newPosition - _buffered)).ConfigureAwait(false);
            }

            await _readBucket.SeekAsync(newPosition).ConfigureAwait(false);
        }

        public override bool CanReset => _readBucket.CanReset;

        private sealed class MyWriter : IBucketWriter
        {
            private readonly BufferBucket _bufb;
            public MyWriter(BufferBucket bufb)
            {
                _bufb = bufb;
            }

            public ValueTask ShutdownAsync()
            {
                return default;
            }

            public void Write(Bucket bucket)
            {
                (_bufb._readBucket as AggregateBucket)!.Append(bucket);
            }
        }
    }
}
