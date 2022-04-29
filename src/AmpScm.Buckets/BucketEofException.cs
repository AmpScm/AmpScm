using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets
{
    [Serializable]
    public class BucketEofException : BucketException
    {
        public BucketEofException()
        {
        }

        public BucketEofException(string? message) : base(message)
        {
        }

        public BucketEofException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected BucketEofException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public static BucketEofException Throw(Bucket bucket)
        {
            if (bucket == null)
                throw new BucketEofException("Unexpected EOF in Bucket");
            else
                throw new BucketEofException($"Unexpected EOF in {bucket.Name} Bucket");
        }
    }
}
