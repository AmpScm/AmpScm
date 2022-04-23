using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Client.Http;

namespace AmpScm.Buckets.Client
{
    public static class BucketHttpClientExtensions
    {
        public static Bucket HttpChunk(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return new HttpChunkBucket(bucket);
        }

        public static Bucket HttpDechunk(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return new HttpDechunkBucket(bucket);
        }
    }
}
