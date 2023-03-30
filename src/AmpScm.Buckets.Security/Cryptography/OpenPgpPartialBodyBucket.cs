using System;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Cryptography
{
    internal sealed class OpenPgpPartialBodyBucket : WrappingBucket
    {
        private int _remaining;
        private bool _final;

        public OpenPgpPartialBodyBucket(Bucket source, uint firstLength)
            : base(source)
        {
            _remaining = (int)firstLength;
        }

        public override BucketBytes Peek()
        {
            var b = Source.Peek();

            if (b.Length > _remaining)
            {
                return b.Slice(0, _remaining);
            }

            return b;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
        {
            if (requested < 1)
                throw new ArgumentOutOfRangeException(nameof(requested), requested, null);

            if (_remaining == 0 && !_final)
            {
                var (len, partial) = await OpenPgpContainer.ReadLengthAsync(Source).ConfigureAwait(false);

                _final = !partial;
                _remaining = (int)len!.Value;
            }

            if (_remaining > 0)
            {
                var bb = await Source.ReadAsync(Math.Min(requested, _remaining)).ConfigureAwait(false);

                _remaining -= bb.Length;
                return bb;
            }
            else
                return BucketBytes.Eof;
        }
    }
}
