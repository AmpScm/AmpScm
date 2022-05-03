using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git;

namespace AmpScm.Buckets.Git
{
    public abstract class GitObjectBucket : GitBucket
    {
        protected GitObjectBucket(Bucket inner) : base(inner)
        {
        }

        public GitObjectType Type { get; protected set; }

        public abstract ValueTask<GitObjectType> ReadTypeAsync();
    }
}
