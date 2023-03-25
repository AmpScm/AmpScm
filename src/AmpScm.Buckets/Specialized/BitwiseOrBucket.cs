using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    public class BitwiseOrBucket : CombineBucket
    {
        private readonly byte[] _buffer;
        private BucketBytes _bbLeft;
        private BucketBytes _bbRight;

        public BitwiseOrBucket(Bucket left, Bucket right) : base(left, right)
        {
            _buffer = new byte[4096];
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            if (requested > _buffer.Length)
                requested = _buffer.Length;

            if (!_bbLeft.IsEmpty || !_bbRight.IsEmpty)
            {
                if (_bbLeft.IsEmpty && !_bbLeft.IsEof)
                    _bbLeft = await LeftSource.ReadAsync(Math.Max(_bbRight.Length, 1)).ConfigureAwait(false);

                if (_bbRight.IsEmpty && !_bbRight.IsEof)
                    _bbRight = await RightSource.ReadAsync(Math.Max(_bbRight.Length, 1)).ConfigureAwait(false);
            }
            else
            {
                _bbLeft = await LeftSource.ReadAsync(requested).ConfigureAwait(false);
                _bbRight = await RightSource.ReadAsync(_bbLeft.IsEmpty ? requested : _bbLeft.Length).ConfigureAwait(false);
            }

            if (_bbLeft.IsEof)
            {
                if (_bbRight.IsEof)
                    return BucketBytes.Eof;

                // Assume left is all 0, so we can return right
                if (requested >= _bbRight.Length)
                {
                    var r = _bbRight;
                    _bbRight = BucketBytes.Empty;
                    return r;
                }
                else
                {
                    var r = _bbRight.Slice(0, requested);
                    _bbRight = _bbRight.Slice(requested);
                    return r;
                }
            }
            else if (_bbRight.IsEof)
            {
                // Assume right is all 0, so we can return left
                if (requested >= _bbLeft.Length)
                {
                    var r = _bbLeft;
                    _bbLeft = BucketBytes.Empty;
                    return r;
                }
                else
                {
                    var r = _bbLeft.Slice(0, requested);
                    _bbLeft = _bbLeft.Slice(requested);
                    return r;
                }
            }
            else
            {
                int got = Process();

                if (got == _bbLeft.Length)
                    _bbLeft = BucketBytes.Empty;
                else
                    _bbLeft = _bbLeft.Slice(got);

                if (got == _bbRight.Length)
                    _bbRight = BucketBytes.Empty;
                else
                    _bbRight = _bbRight.Slice(got);

                return new BucketBytes(_buffer, 0, got);
            }
        }

        public override BucketBytes Peek()
        {
            // TODO: Check if both sides have something to peek
            return base.Peek();
        }

        public override ValueTask<long> ReadSkipAsync(long requested)
        {
            // TODO: Skip on both sides
            return base.ReadSkipAsync(requested);
        }

        private int Process()
        {
            int got = Math.Min(_bbLeft.Length, _bbRight.Length);

            // TODO: Optimize with vector operations...
            for (int i = 0; i < got; i++)
                _buffer[i] = (byte)(_bbLeft[i] | _bbRight[i]);

            return got;
        }

        public override string Name => $"{BaseName}>[{LeftSource.Name}],[{RightSource.Name}]";

        public override bool CanReset => LeftSource.CanReset && RightSource.CanReset;

        public override void Reset()
        {
            LeftSource.Reset();
            RightSource.Reset();

            _bbLeft = _bbRight = BucketBytes.Empty;
        }

        public override long? Position => null;

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            long? l1 = await LeftSource.ReadRemainingBytesAsync().ConfigureAwait(false);
            long? l2 = await RightSource.ReadRemainingBytesAsync().ConfigureAwait(false);

            if (l1.HasValue && l2.HasValue)
                return Math.Max(l1.Value, l2.Value);
            else
                return null;
        }
    }
}
