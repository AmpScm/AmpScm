using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using AmpScm.Git;

namespace AmpScm.Buckets.Git
{
    public abstract class GitObjectBucket : GitBucket, IBucketSeek
    {
        protected GitObjectBucket(Bucket inner) : base(inner)
        {
        }

        public abstract ValueTask<GitObjectType> ReadTypeAsync();

        public abstract ValueTask SeekAsync(long newPosition);
    }
}
