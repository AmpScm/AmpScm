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

        /// <summary>
        /// Constructs a <see cref="BucketEofException"/> for an EOF on the <paramref name="bucket"/> Bucket.
        /// </summary>
        /// <param name="bucket"></param>
        public BucketEofException(Bucket bucket)
            : this($"Unexpected EOF in {bucket?.Name ?? "NULL"} Bucket")
        {
        }
    }
}
