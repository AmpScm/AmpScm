using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets
{
    [Serializable]
    public class BucketException : Exception
    {
        public BucketException()
        {
        }

        public BucketException(string? message) : base(message)
        {
        }

        public BucketException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        [Obsolete("Just for legacy .Net compatibilty")]
        protected BucketException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
