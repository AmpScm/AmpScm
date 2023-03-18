using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    sealed class BufferBucket : WrappingBucket, IBucketSeek, IBucketPoll
    {
        readonly int _maxRam;
        Bucket readBucket;
        IBucketWriter _writer;
        long _buffered;
        long? _size;
        bool _readEof;
        bool _disposed;

        public BufferBucket(Bucket inner, int maxMemory = 0)
            : base(inner)
        {
            if (maxMemory < 0)
                throw new ArgumentOutOfRangeException(nameof(maxMemory));

            _maxRam = maxMemory;
            readBucket = new AggregateBucket(true, Array.Empty<Bucket>());
            _writer = new MyWriter(this);
        }

        public async override ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            var bb = await readBucket.ReadAsync(requested).ConfigureAwait(false);
            if (!bb.IsEof || _readEof)
                return bb;

            await BufferSomeMore(requested).ConfigureAwait(false);

            return await readBucket.ReadAsync(requested).ConfigureAwait(false);
        }

        private async ValueTask BufferSomeMore(int requested)
        {
            if (_readEof)
                return;

            var bb = await Inner.ReadAsync(requested).ConfigureAwait(false);

            if (!bb.IsEmpty)
            {
                _buffered += bb.Length;
                _writer.Write(bb.ToArray().AsBucket());
            }
            else
            {
                _size = _buffered;
                _readEof = true;
                InnerDispose();
            }
        }

        public override BucketBytes Peek()
        {
            var bb = readBucket.Peek();

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

            return await readBucket.PollAsync(minRequested).ConfigureAwait(false);
        }

        protected override void InnerDispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                base.InnerDispose();
            }
        }

        public override long? Position => readBucket.Position;

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            if (_size is null)
            {
                _size = await Inner.ReadRemainingBytesAsync().ConfigureAwait(false);

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
            readBucket.Reset();
        }

        public async ValueTask SeekAsync(long newPosition)
        {
            while(newPosition > _buffered && !_readEof)
            {
                await BufferSomeMore((int)(newPosition - _buffered)).ConfigureAwait(false);
            }

            await readBucket.SeekAsync(newPosition).ConfigureAwait(false);
        }

        public override bool CanReset => readBucket.CanReset;

        sealed class MyWriter : IBucketWriter
        {
            BufferBucket _bufb;
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
                (_bufb.readBucket as AggregateBucket)!.Append(bucket);
            }
        }
    }
}
