using System;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets
{
    public class MemoryBucket : Bucket, IBucketNoClose, IBucketReadBuffers
    {
        BucketBytes _data;
        int _offset;

        public MemoryBucket(byte[] data)
        {
            _data = data ?? Array.Empty<byte>();
        }

        public MemoryBucket(byte[] data, int start, int length)
        {
            _data = new ReadOnlyMemory<byte>(data, start, length);
        }

        public MemoryBucket(ReadOnlyMemory<byte> data)
        {
            _data = data;
        }

        public override BucketBytes Peek()
        {
            return _data.Slice(_offset);
        }

        public override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            int canRead = Math.Min(requested, _data.Length - _offset);

            if (canRead == 0 && requested > 0)
                return BucketBytes.Eof;

            var r = _data.Slice(_offset, canRead);
            _offset += r.Length;

            return r;
        }

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            return new ValueTask<long?>(_data.Length - _offset);
        }

        public override ValueTask<Bucket> DuplicateAsync(bool reset = false)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            var mb = new MemoryBucket(_data.Memory);
#pragma warning restore CA2000 // Dispose objects before losing scope
            if (!reset)
                mb._offset = _offset;

            return new ValueTask<Bucket>(mb);
        }

        public override long? Position => _offset;

        public override bool CanReset => true;

        public override ValueTask ResetAsync()
        {
            _offset = 0;

            return new ValueTask();
        }

#pragma warning disable CA1033 // Interface methods should be callable by child types
        Bucket IBucketNoClose.NoClose()
#pragma warning restore CA1033 // Interface methods should be callable by child types
        {
            return this;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable CA1033 // Interface methods should be callable by child types
        async ValueTask<(ReadOnlyMemory<byte>[] Buffers, bool Done)> IBucketReadBuffers.ReadBuffersAsync(int maxRequested)
#pragma warning restore CA1033 // Interface methods should be callable by child types
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (maxRequested >= _data.Length - _offset)
            {
                ReadOnlyMemory<byte>[] r = new[] { _data.Memory.Slice(_offset, _data.Length - _offset) };

                _offset = _data.Length;

                return (r, true);
            }
            else
            {
                ReadOnlyMemory<byte>[] r = new[] { _data.Memory.Slice(_offset, maxRequested) };

                _offset += maxRequested;

                return (r, false);
            }
        }

        internal BucketBytes Data => _data;
        internal int Offset => _offset;
    }
}
