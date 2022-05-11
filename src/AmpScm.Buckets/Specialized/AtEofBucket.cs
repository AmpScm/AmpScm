using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    sealed class AtEofBucket : ProxyBucket.WithPoll
    {
        Func<Task>? _atEof;
        public AtEofBucket(Bucket inner, Func<Task> atEof) : base(inner)
        {
            _atEof = atEof;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            var bb = await base.ReadAsync(requested).ConfigureAwait(false);

            if (bb.IsEof)
            {
                _atEof?.Invoke();
                _atEof = null;
            }
            return bb;
        }

        public override bool CanReset => false;
    }
}
