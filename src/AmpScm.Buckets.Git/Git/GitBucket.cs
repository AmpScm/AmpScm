using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Git
{
    public abstract class GitBucket : WrappingBucket
    {
        protected GitBucket(Bucket inner) : base(inner)
        {
        }
    }
}
