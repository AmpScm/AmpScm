using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets
{
    partial struct BucketBytes
    {
        public string ToASCIIString()
        {
            return Encoding.ASCII.GetString(Span);
        }

        public string ToASCIIString(int position, int length)
        {
            var data = Span.Slice(position, length);

            return Encoding.ASCII.GetString(data);
        }

        public string ToASCIIString(int position, int length, BucketEol eol)
        {
            return ToASCIIString(position, length - eol.CharCount());
        }

        public string ToASCIIString(BucketEol eol)
        {
            return ToASCIIString(0, Length - eol.CharCount());
        }

        public string ToUTF8String()
        {
            return Encoding.UTF8.GetString(Span);
        }

        public string ToUTF8String(int position, int length)
        {
            var data = Span.Slice(position, length);

            return Encoding.UTF8.GetString(data);
        }

        public string ToUTF8String(int position, int length, BucketEol eol)
        {
            return ToUTF8String(position, length - eol.CharCount());
        }

        public string ToUTF8String(BucketEol eol)
        {
            return ToUTF8String(0, Length - eol.CharCount());
        }

        public bool StartsWithASCII(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentNullException(nameof(value));

            var p = _data.Span;

            if (p.Length < value.Length)
                return false;

            for(int i = 0; i < value.Length; i++)
            {
                if (p[i] != value[i])
                    return false;
            }

            return true;
        }

    }
}
