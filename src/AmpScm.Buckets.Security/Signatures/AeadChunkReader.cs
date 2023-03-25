using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Signatures
{
    internal class AeadChunkReader : WrappingBucket
    {
#pragma warning disable CA2213 // Disposable fields should be disposed
        private Bucket? _current; // Disposed via inner
        private readonly Func<int, Bucket, Bucket> _createAdditional;
        private int _iChunkNumber;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private bool _done;

        public AeadChunkReader(Bucket inner, int maxChunkSize, int tagSize, Func<int, Bucket, Bucket> createAdditional) 
            : base(inner)
        {
            if (maxChunkSize < 16)
                throw new ArgumentOutOfRangeException(nameof(maxChunkSize), maxChunkSize, message: null);
            else if (tagSize < 1)
                throw new ArgumentOutOfRangeException(nameof(tagSize), tagSize, message: null);

            _createAdditional = createAdditional;
            MaxChunkSize = maxChunkSize;
            TagSize = tagSize;
        }

        public int MaxChunkSize { get; }
        public int TagSize { get; }


        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            while (!_done)
            {
                bool createdNew = false;
                if (_current == null)
                {
                    NewCurrent();
                    createdNew = true;
                }

                var bb = await _current.ReadAsync(requested).ConfigureAwait(false);

                if (bb.IsEmpty)
                {
                    _current = null;
                    if (bb.IsEof && createdNew)
                    {
                        _done = true;
                        return bb; // EOF
                    }
                }
                else
                    return bb;
            }

            return BucketBytes.Eof;
        }

        private void NewCurrent()
        {
            _current = Source.Take(MaxChunkSize + TagSize); // Take, not TakeExactly!

            _current = _createAdditional(_iChunkNumber++, _current) ?? _current;
        }

        public override BucketBytes Peek()
        {
            if (_done)
                return BucketBytes.Eof;
            else if (_current is not { })
                NewCurrent();
            
            return _current.Peek();
        }

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            // TODO: If the inner bucket returns result, we should be able to calculate the result
            return base.ReadRemainingBytesAsync();
        }
    }
}
