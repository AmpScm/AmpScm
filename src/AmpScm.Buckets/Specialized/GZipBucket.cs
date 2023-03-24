using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class GZipBucket : WrappingBucket, IBucketPoll, IBucketSeek
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _readHeader;
        private bool _atEof;

        public GZipBucket(Bucket inner, CompressionMode mode, BucketCompressionLevel level = BucketCompressionLevel.Default, int bufferSize = ZLibBucket.DefaultBufferSize)
            : base(new ZLibBucket(inner, BucketCompressionAlgorithm.Deflate, mode, level))
        {
            if (mode != CompressionMode.Decompress)
                throw new NotImplementedException();

            _readHeader = true;
        }

        private GZipBucket(Bucket inner)
            : base(inner)
        {

        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            if (_readHeader)
                await ReadHeader().ConfigureAwait(false);

            if (_atEof)
                return BucketBytes.Eof;

            BucketBytes bb;

            bb = await Inner.ReadAsync(requested).ConfigureAwait(false);

            if (!bb.IsEof)
                return bb;

            bb = await ((ZLibBucket)Inner).GetInner().ReadExactlyAsync(8).ConfigureAwait(false);

            if (bb.Length != 8)
                throw new BucketEofException(this);

            // Handle endiannes agnostic
            int expectedLen = BinaryPrimitives.ReadInt32LittleEndian(bb.Span.Slice(4));

            if (expectedLen != (Inner.Position!.Value & uint.MaxValue))
                throw new BucketException($"GZip error: Expected {expectedLen} bytes, but read {Inner.Position}");

            _atEof = true;
            return BucketBytes.Eof;
        }

        private async ValueTask ReadHeader()
        {
            var bb = await ((ZLibBucket)Inner).GetInner().ReadExactlyAsync(10).ConfigureAwait(false);

            if (bb.Length == 0)
            {
                _atEof = true;
                return;
            }

            if (bb.Length != 10)
                throw new BucketEofException(this);

            if (bb[0] != 0x1F || bb[1] != 0x8B)
                throw new BucketException($"Expected GZip header at start of {Name} Bucket");

            _readHeader = false;
        }

        public async ValueTask<BucketBytes> PollAsync(int minRequested = 1)
        {
            if (_readHeader)
                await ReadHeader().ConfigureAwait(false);

            if (_atEof)
                return BucketBytes.Eof;

            return await ((IBucketPoll)Inner).PollAsync(minRequested).ConfigureAwait(false);
        }

        public override BucketBytes Peek()
        {
            if (_readHeader)
                return BucketBytes.Empty;

            return Inner.Peek();
        }

        public async ValueTask SeekAsync(long newPosition)
        {
            if (_readHeader)
                await ReadHeader().ConfigureAwait(false);

            await ((IBucketSeek)Inner).SeekAsync(newPosition).ConfigureAwait(false);
            _atEof = false;
        }

        public override Bucket Duplicate(bool reset = false)
        {
            if (_readHeader)
                throw new InvalidOperationException();

            var b = Inner.Duplicate(reset);
            var gz = new GZipBucket(b);

            gz._readHeader = false;

            return gz;
        }

        public override bool CanReset => Inner.CanReset;

        public override void Reset()
        {
            if (_readHeader) // Nothing to reset
                return;

            Inner.Reset();
            _atEof = false;
        }

        public override long? Position => Inner.Position;

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            return Inner.ReadRemainingBytesAsync();
        }
    }
}
