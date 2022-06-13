using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    public abstract class ConversionBucket : WrappingBucket//, IBucketPoll
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        BucketBytes _remaining;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        int _skipFirst;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        BucketBytes _readLeft;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        long _position;

        protected ConversionBucket(Bucket inner) : base(inner)
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

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
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
                    _readLeft = await InnerReadAsync(ConvertRequested(requested)).ConfigureAwait(false);

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

        protected virtual ValueTask<BucketBytes> InnerReadAsync(int requested = MaxRead)
            => Inner.ReadAsync(requested);

        protected virtual int ConvertRequested(int requested)
        {
            return requested;
        }

        public override void Reset()
        {
            if (!CanReset)
                throw new InvalidOperationException();

            Inner.Reset();
            _position = 0;
        }

        public override long? Position => _position;
    }
}
