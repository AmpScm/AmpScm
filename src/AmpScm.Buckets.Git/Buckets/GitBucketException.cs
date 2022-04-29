using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git;

namespace AmpScm.Buckets.Git
{
    [Serializable]
    public class GitBucketException : GitException
    {
        public GitBucketException()
        {
        }

        public GitBucketException(string message)
            : base(message)
        {


        }

        public GitBucketException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected GitBucketException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
