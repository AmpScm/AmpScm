using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    public sealed class BitwiseNotBucket : WrappingBucket
    {
        private readonly byte[] _buffer;

        public BitwiseNotBucket(Bucket inner, int bufferSize=4096)
            : base(inner)
        {
            _buffer = new byte[bufferSize];
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            if (requested > _buffer.Length)
                requested = _buffer.Length;

            var bb = await Inner.ReadAsync(requested).ConfigureAwait(false);

            if (bb.IsEmpty)
                return bb; // Includes EOF

            for(int i = 0; i < bb.Length; i++)
            {
                _buffer[i] = (byte)~bb[i];
            }

            return new BucketBytes(_buffer, 0, bb.Length);
        }

        public override ValueTask<long> ReadSkipAsync(long requested)
        {
            return Inner.ReadSkipAsync(requested);
        }

        public override BucketBytes Peek()
        {
            var bb = Inner.Peek();

            if (bb.IsEmpty)
                return bb; // Includes EOF

            int use = Math.Min(bb.Length, 256);

            for (int i = 0; i < use; i++)
            {
                _buffer[i] = (byte)~bb[i];
            }

            return new BucketBytes(_buffer, 0, use);
        }

        public override long? Position => Inner.Position;

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            return Inner.ReadRemainingBytesAsync();
        }
    }
}
