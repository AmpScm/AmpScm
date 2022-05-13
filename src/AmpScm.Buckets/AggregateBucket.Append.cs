using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets
{
    public partial class AggregateBucket
    {
        internal sealed class Simple : AggregateBucket, IBucketPoll
        {
            public Simple(params Bucket[] items)
                : base(items)
            {
            }

            public Simple(bool keepOpen, params Bucket[] items)
                : base(keepOpen, items)
            {

            }

            public async ValueTask<BucketBytes> PollAsync(int minRequested = 1)
            {
                while (true)
                {
                    if (CurrentBucket is Bucket cur)
                    {
                        var v = await cur.PollAsync(minRequested).ConfigureAwait(false);

                        if (!v.IsEof)
                            return v;
                    }
                    else
                        return BucketBytes.Eof;


                    lock (LockOn)
                    {
                        if (!_keepOpen)
                        {
                            try
                            {
                                _buckets[_n]?.Dispose();
                            }
                            finally
                            {
                                _buckets[_n] = null;
                            }
                        }
                        _n++;
                    }
                }
            }
        }

    }
}
