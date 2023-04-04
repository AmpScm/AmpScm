using System;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class AlsoReadBucket : WrappingBucket
    {
        private Func<BucketBytes, ValueTask>? _reader;

        public AlsoReadBucket(Bucket source, Func<BucketBytes, ValueTask> reader)
            : base(source)
        {
            _reader = reader;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            var bb = await Source.ReadAsync(requested).ConfigureAwait(false);

            if (!bb.IsEmpty)
            {
                if (_reader != null)
                    await _reader(bb).ConfigureAwait(false);
            }
            else
            {
                if (_reader != null)
                    await _reader(BucketBytes.Eof).ConfigureAwait(false);

                _reader = null;
            }

            return bb;
        }

        public override long? Position => Source.Position;

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            return Source.ReadRemainingBytesAsync();
        }

        public override BucketBytes Peek()
        {
            return Source.Peek();
        }
    }
}
