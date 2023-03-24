using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    public abstract class BlockCombineBucket : CombineBucket
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private BucketBytes _bbLeft;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private BucketBytes _bbRight;

        protected BlockCombineBucket(Bucket left, Bucket right) : base(left, right)
        {
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            if (!_bbLeft.IsEmpty || !_bbRight.IsEmpty)
            {
                if (_bbLeft.IsEmpty && !_bbLeft.IsEof)
                    _bbLeft = await Left.ReadAsync(Math.Max(_bbRight.Length, 1)).ConfigureAwait(false);

                if (_bbRight.IsEmpty && !_bbRight.IsEof)
                    _bbRight = await Right.ReadAsync(Math.Max(_bbRight.Length, 1)).ConfigureAwait(false);
            }
            else
            {
                _bbLeft = await Left.ReadAsync(requested).ConfigureAwait(false);
                _bbRight = await Right.ReadAsync(_bbLeft.IsEmpty ? requested : _bbLeft.Length).ConfigureAwait(false);
            }

            if (_bbLeft.IsEof)
            {
                if (_bbRight.IsEof)
                    return BucketBytes.Eof;

                throw new BucketEofException();
            }
            else if (_bbRight.IsEof)
            {
                throw new BucketEofException();
            }
            else
            {
                int haveBoth = Math.Min(_bbLeft.Length, _bbRight.Length);
                var bb = await ProcessAsync(_bbLeft.Slice(0, haveBoth), _bbRight.Slice(0, haveBoth)).ConfigureAwait(false);
                int got = bb.Length;

                if (got == _bbLeft.Length)
                    _bbLeft = BucketBytes.Empty;
                else
                    _bbLeft = _bbLeft.Slice(got);

                if (got == _bbRight.Length)
                    _bbRight = BucketBytes.Empty;
                else
                    _bbRight = _bbRight.Slice(got);

                return bb;
            }
        }

        protected abstract ValueTask<BucketBytes> ProcessAsync(BucketBytes left, BucketBytes right);
    }
}
