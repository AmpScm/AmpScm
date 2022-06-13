using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Git
{
    public sealed class GitLineUnindentBucket : WrappingBucket
    {
        readonly BucketEol _acceptableEols;
        bool _cont;
        bool _eof;

        public GitLineUnindentBucket(Bucket inner, BucketEol acceptableEols = BucketEol.LF) : base(inner)
        {
            _acceptableEols = acceptableEols;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
        {
            BucketBytes bb;

            if (_eof)
                return BucketBytes.Eof;

            if (!_cont)
            {
                bb = await Inner.PollAsync().ConfigureAwait(false);

                if (!bb.IsEmpty)
                {
                    if (bb[0] == ' ')
                    {
                        bb = await Inner.ReadAsync(1).ConfigureAwait(false);

                        if (bb.IsEof)
                            throw new BucketEofException(Inner);
                        _cont = true;
                    }
                    else
                    {
                        _eof = true;
                        return BucketBytes.Eof;
                    }
                }
            }

            BucketEol eol;

            (bb, eol) = await Inner.ReadUntilEolAsync(_acceptableEols, requested).ConfigureAwait(false);

            if (eol != BucketEol.None && eol != BucketEol.CRSplit)
                _cont = false;
            else if (bb.IsEof)
                _eof = true;

            return bb;
        }

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            var bb = await Inner.PollAsync().ConfigureAwait(false);

            return CalcLen(bb, _cont);

            static long? CalcLen(BucketBytes bb, bool cont)
            {
                var span = bb.Span;
                long len = 0;

                if (!cont)
                {
                    if (span.Length > 0 && span[0] == ' ')
                    {
                        span = span.Slice(1);
                    }
                    else
                        return null;
                }

                int n = span.IndexOf((byte)'\n');
                while (n >= 0)
                {
                    len += n + 1;

                    if (n + 1 < span.Length)
                    {
                        if (span[n + 1] == ' ')
                            span = span.Slice(n + 2);
                        else
                            break;
                    }
                    else
                        return null;

                    n = span.IndexOf((byte)'\n');
                }

                if (n < 0)
                    return null;

                return len;
            }
        }

        public override BucketBytes Peek()
        {
            if (_eof)
                return BucketBytes.Eof;

            var bb = Inner.Peek();

            if (bb.IsEmpty)
                return bb;

            if (!_cont)
            {
                if (bb[0] == ' ')
                    bb = bb.Slice(1);
                else
                    return BucketBytes.Empty;
            }

            int n = bb.IndexOfAny((byte)'\n', (byte)'\r', (byte)'\0');

            if (n >= 0)
                return bb.Slice(0, n);
            else
                return bb;
        }
    }
}
