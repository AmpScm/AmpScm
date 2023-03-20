using System;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Subversion
{
    sealed class SvnKeyValueHeaderBucket : SvnBucket
    {
        Action? _atEof;

        public SvnKeyValueHeaderBucket(Bucket inner, Action? atEof=null)
            : base(inner)
        {
            _atEof = atEof ?? (() => { });
        }

        public async ValueTask<(BucketBytes Key, BucketBytes Value)?> ReadKeyAsync()
        {
            if (_atEof is null)
                return null;

            var (bb, eol) = await Inner.ReadExactlyUntilEolAsync(BucketEol.LF).ConfigureAwait(false);

            if (bb.IsEof || bb.Slice(eol).IsEmpty)
            {
                _atEof.Invoke();
                _atEof = null;
                return null;
            }

            var parts = bb.Split((byte)':', 2);

            if (parts.Length == 2)
            {
                return (parts[0], parts[1].Slice(eol).TrimStart());
            }
            else
                return null;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
        {
            while(await ReadKeyAsync().ConfigureAwait(false) is (var key, var value))
            {
                Console.WriteLine($"{key.ToASCIIString()}: {value.ToASCIIString()}");
            }

            return BucketBytes.Eof;
        }
    }
}
