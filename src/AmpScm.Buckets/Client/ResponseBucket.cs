﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Client
{
    public abstract class ResponseBucket : WrappingBucket
    {
        protected ResponseBucket(Bucket inner, BucketWebRequest request)
            : base(inner, true)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public BucketWebRequest Request { get; }


        public virtual ValueTask ReadHeaders()
        {
            return default;
        }

        public virtual WebHeaderDictionary Headers => throw new NotSupportedException();
        public virtual bool SupportsHeaders => false;

        public virtual string? ContentType => null;

        public virtual long ContentLength => throw new NotSupportedException();
    }
}
