using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Git
{
    public sealed class GitLineIndentBucket : WrappingBucket
    {
        readonly BucketEol _acceptableEols;
        static readonly ReadOnlyMemory<byte> _indentData = new byte[] { (byte)' ' };
        bool _indent;
        BucketBytes _next;
        bool _lastWasEol;

        public GitLineIndentBucket(Bucket inner, BucketEol acceptableEols = BucketEol.LF) : base(inner)
        {
            _acceptableEols = acceptableEols;
            _next = _indentData;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = Bucket.MaxRead)
        {
            if (!_next.IsEmpty)
            {
                int r = Math.Min(_next.Length, requested);

                try
                {
                    return _next.Slice(0, r);
                }
                finally
                {
                    _next = _next.Slice(r);
                }
            }

            if (_indent)
            {
                var (bb, eol) = await Source.ReadUntilEolAsync(_acceptableEols, requested).ConfigureAwait(false);

                if (bb.IsEof)
                {
                    if (!_lastWasEol)
                    {
                        if ((_acceptableEols & BucketEol.LF) != 0)
                            bb = new byte[] { (byte) '\n' };
                        else
                            throw new NotImplementedException();
                        _lastWasEol = true;
                        return bb;
                    }
                    return bb;
                }

                _next = bb;
                if (eol != BucketEol.None && eol != BucketEol.CRSplit)
                {
                    _indent = true;
                    _lastWasEol = true;
                }
                else
                    _lastWasEol = false;

                return _indentData;
            }
            else
            {
                var (bb, eol) = await Source.ReadUntilEolAsync(_acceptableEols, requested).ConfigureAwait(false);

                if (eol != BucketEol.None && eol != BucketEol.CRSplit)
                {
                    _indent = true;
                    _lastWasEol = true;
                }
                else
                    _lastWasEol = false;

                return bb;
            }
        }

        public override BucketBytes Peek()
        {
            if (!_next.IsEmpty)
                return _next;
            else if (_indent)
                return _indentData;
            else
            {
                var bb = Source.Peek();

                int n = bb.IndexOfAny((byte)'\n', (byte)'\r', (byte)'\0');

                if (n >= 0)
                    return bb.Slice(0, n);
                else
                    return bb;
            }
        }
    }
}
