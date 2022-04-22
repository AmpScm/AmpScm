using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    public abstract class ConvertBucket : WrappingBucket//, IBucketPoll
    {
        BucketBytes _remaining;
        int _skipFirst;
        BucketBytes _readLeft;
        long _position;

        protected ConvertBucket(Bucket inner) : base(inner)
        {
        }

        //public ValueTask<BucketBytes> PollAsync(int minRequested = 1)
        //{
        //    return Peek();
        //}

        public override BucketBytes Peek()
        {
            if (!_remaining.IsEmpty)
                return _remaining;

            while (!_readLeft.IsEmpty)
            {
                int origLeft = _readLeft.Length;
                _remaining = ConvertData(ref _readLeft, false);

                if (!_remaining.IsEmpty || _readLeft.Length == origLeft)
                    return _remaining;
            }

            var bb = Inner.Peek();

            if (!bb.IsEmpty && _skipFirst > 0)
                bb = bb.Slice(_skipFirst);

            if (bb.IsEmpty)
                return bb;

            int len = bb.Length;
            _remaining = ConvertData(ref bb, false);

            _skipFirst += (len - bb.Length);

            return _remaining;
        }

        protected abstract BucketBytes ConvertData(ref BucketBytes sourceData, bool final);

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (!_remaining.IsEmpty)
            {
                var r = _remaining.Slice(0, Math.Min(requested, _remaining.Length));
                _remaining = _remaining.Slice(r.Length);
                _position += r.Length;
                return r;
            }
            while (_skipFirst > 0)
            {
                var skipped = await Inner.ReadSkipAsync(_skipFirst).ConfigureAwait(false);
                _skipFirst -= (int)skipped;
            }

            do
            {
                if (_readLeft.IsEmpty)
                    _readLeft = await Inner.ReadAsync(ConvertRequested(requested)).ConfigureAwait(false);

                _remaining = ConvertData(ref _readLeft, _readLeft.IsEof);

                if (!_remaining.IsEmpty)
                {
                    var r = _remaining.Slice(0, Math.Min(requested, _remaining.Length));
                    _remaining = _remaining.Slice(r.Length);
                    _position += r.Length;
                    return r;
                }
            }
            while (!_readLeft.IsEof);

            return BucketBytes.Eof;
        }

        protected virtual int ConvertRequested(int requested)
        {
            return requested;
        }

        public override async ValueTask ResetAsync()
        {
            if (!CanReset)
                throw new InvalidOperationException();

            await Inner.ResetAsync().ConfigureAwait(false);
            _position = 0;
        }

        public override long? Position => _position;
    }
}
