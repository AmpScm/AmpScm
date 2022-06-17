using System;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    sealed class AlsoReadBucket : WrappingBucket
    {
        Func<BucketBytes, ValueTask>? _reader;

        public AlsoReadBucket(Bucket inner, Func<BucketBytes, ValueTask> reader)
            : base(inner)
        {
            _reader = reader;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
        {
            var bb = await Inner.ReadAsync(requested).ConfigureAwait(false);

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

        public override long? Position => Inner.Position;

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            return Inner.ReadRemainingBytesAsync();
        }
    }
}
