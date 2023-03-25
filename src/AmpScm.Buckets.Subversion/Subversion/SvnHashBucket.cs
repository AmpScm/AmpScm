using System;
using System.Globalization;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Subversion
{
    internal sealed class SvnHashBucket : SvnBucket
    {
        private Action? _atEof;
        bool _readEol;

        public SvnHashBucket(Bucket inner, Action? atEof = null)
            : base(inner)
        {
            this._atEof = atEof ?? (() => { });
        }

        public async ValueTask<(string key, BucketBytes value)?> ReadValue()
        {
            if (_atEof == null)
                return null;
            var (bb, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.LF).ConfigureAwait(false);

            if (bb.IsEof)
                return null;
            else if (_readEol)
            {
                _readEol = false;
                if (bb.Length > eol.CharCount())
                    throw new BucketException();

                (bb, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.LF).ConfigureAwait(false);
            }

            if (!bb.StartsWithASCII("K "))
            {
                if (bb.StartsWithASCII("END") && bb.Slice(3, eol).IsEmpty)
                {
                    _atEof.Invoke();
                    _atEof = null;
                    return null;
                }

                throw new BucketException();
            }

            if (!int.TryParse(bb.Slice(2, eol).ToASCIIString(), System.Globalization.NumberStyles.Number, CultureInfo.InvariantCulture, out var len))
                throw new BucketException();

            bb = await Source.ReadExactlyAsync(len).ConfigureAwait(false);

            string key = bb.ToUTF8String();

            (bb, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.LF, 1).ConfigureAwait(false);

            if (bb.Length > eol.CharCount())
                throw new BucketException();

            (bb, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.LF).ConfigureAwait(false);

            if (!bb.StartsWithASCII("V "))
                throw new BucketException();

            if (!int.TryParse(bb.Slice(2, eol).ToASCIIString(), System.Globalization.NumberStyles.Number, CultureInfo.InvariantCulture, out len))
                throw new BucketException();

            bb = await Source.ReadExactlyAsync(len).ConfigureAwait(false);

            if (bb.Length == len)
            {
                _readEol = true;
                return (key, bb);
            }

            return null;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
        {
            while (await ReadValue().ConfigureAwait(false) is (var key, _))
            {

            }

            return BucketBytes.Eof;
        }
    }
}
