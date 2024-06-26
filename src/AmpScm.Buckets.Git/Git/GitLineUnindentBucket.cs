﻿using AmpScm.Buckets;

namespace AmpScm.Buckets.Git;

public sealed class GitLineUnindentBucket : WrappingBucket
{
    private readonly BucketEol _acceptableEols;
    private bool _cont;
    private bool _eof;

    public GitLineUnindentBucket(Bucket source, BucketEol acceptableEols = BucketEol.LF) : base(source)
    {
        _acceptableEols = acceptableEols;
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        BucketBytes bb;

        if (_eof)
            return BucketBytes.Eof;

        if (!_cont)
        {
            bb = await Source.PollAsync().ConfigureAwait(false);

            if (!bb.IsEmpty)
            {
                if (bb[0] == ' ')
                {
                    bb = await Source.ReadAsync(1).ConfigureAwait(false);

                    if (bb.IsEof)
                        throw new BucketEofException(Source);
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

        (bb, eol) = await Source.ReadUntilEolAsync(_acceptableEols, requested).ConfigureAwait(false);

        if (eol != BucketEol.None && eol != BucketEol.CRSplit)
            _cont = false;
        else if (bb.IsEof)
            _eof = true;

        return bb;
    }

    public override async ValueTask<long?> ReadRemainingBytesAsync()
    {
        var bb = await Source.PollAsync().ConfigureAwait(false);

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

        var bb = Source.Peek();

        if (bb.IsEmpty)
            return bb;

        if (!_cont)
        {
            if (bb[0] == ' ')
                bb = bb.Slice(1);
            else
                return BucketBytes.Empty;
        }

        int n = bb.IndexOf((byte)'\n');

        if (n >= 0)
            return bb.Slice(0, n + 1);
        else
            return bb;
    }
}
