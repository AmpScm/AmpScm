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

        public string ToASCIIString(int start, int length)
        {
            var data = Span.Slice(start, length);

            return Encoding.ASCII.GetString(data);
        }

        public string ToASCIIString(int start, int length, BucketEol eol)
        {
            return ToASCIIString(start, length - eol.CharCount());
        }

        public string ToASCIIString(BucketEol eol)
        {
            return ToASCIIString(0, Length - eol.CharCount());
        }


        public string ToUTF8String()
        {
            return Encoding.UTF8.GetString(Span);
        }

        public string ToUTF8String(int start, int length)
        {
            var data = Span.Slice(start, length);

            return Encoding.UTF8.GetString(data);
        }

        public string ToUTF8String(int start, int length, BucketEol eol)
        {
            return ToUTF8String(start, length - eol.CharCount());
        }

        public string ToUTF8String(BucketEol eol)
        {
            return ToUTF8String(0, Length - eol.CharCount());
        }

        public BucketBytes Trim()
        {
            var data = Span;
            int start = 0;
            int length = data.Length;

            while (length > 0 && IsWhiteSpace(data[start]))
            {
                start++;
                length--;
            }

            while (length > 0 && IsWhiteSpace(data[start + length - 1]))
            {
                length--;
            }

            return Slice(start, length);
        }

        public BucketBytes Trim(BucketEol eol)
        {
            var data = Span;
            int start = 0;
            int length = data.Length - eol.CharCount();

            while (length > 0 && IsWhiteSpace(data[start]))
            {
                start++;
                length--;
            }

            while (length > 0 && IsWhiteSpace(data[start + length - 1]))
            {
                length--;
            }

            return Slice(start, length);
        }

        public BucketBytes TrimEnd()
        {
            var data = Span;
            int length = data.Length;

            while (length > 0 && IsWhiteSpace(data[length - 1]))
            {
                length--;
            }

            return Slice(0, length);
        }

        public BucketBytes TrimEnd(BucketEol eol)
        {
            var data = Span;
            int length = data.Length - eol.CharCount();

            while (length > 0 && IsWhiteSpace(data[length - 1]))
            {
                length--;
            }

            return Slice(0, length);
        }

        public BucketBytes TrimStart()
        {
            var data = Span;
            int start = 0;

            while (start < data.Length && IsWhiteSpace(data[start]))
            {
                start++;
            }

            if (start == 0)
                return this;
            else
                return Slice(start);
        }

        static bool IsWhiteSpace(byte v)
        {
            switch (v)
            {
                case (byte)' ':
                case (byte)'\n':
                case (byte)'\r':
                case (byte)'\t':
                case (byte)'\v':
                    return true;
                default:
                    return false;
            }
        }

        public bool StartsWithASCII(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentNullException(nameof(value));

            var p = _data.Span;

            if (p.Length < value.Length)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                if (p[i] != value[i])
                    return false;
            }

            return true;
        }

    }
}
