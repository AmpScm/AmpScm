using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Git
{
    [Serializable]
    public class GitBucketEofException : BucketEofException
    {
        public GitBucketEofException()
        {
        }

        public GitBucketEofException(string? message) : base(message)
        {
        }

        public GitBucketEofException(string? message, Exception innerException) : base(message, innerException)
        {
        }

        protected GitBucketEofException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        internal GitBucketEofException(Bucket bucket)
            : this($"Unexpected EOF in {bucket?.Name ?? "NULL"} Bucket")
        {
        }
    }
}
