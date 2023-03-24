using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class AtEofBucket : ProxyBucket.WithPoll
    {
        private Func<Task>? _atEof;
        public AtEofBucket(Bucket inner, Func<Task> atEof) : base(inner)
        {
            _atEof = atEof;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            var bb = await base.ReadAsync(requested).ConfigureAwait(false);

            if (bb.IsEof)
            {
                _atEof?.Invoke();
                _atEof = null;
            }
            return bb;
        }

        public override async ValueTask<long> ReadSkipAsync(long requested)
        {
            long r = await base.ReadSkipAsync(requested).ConfigureAwait(false);

            if (r < requested)
            {
                _atEof?.Invoke();
                _atEof = null;
            }

            return r;
        }

        public override bool CanReset => false;
    }
}
