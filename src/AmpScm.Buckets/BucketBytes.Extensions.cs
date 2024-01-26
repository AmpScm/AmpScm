using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets;

public partial struct BucketBytes
{
    public string ToASCIIString()
    {
        return Encoding.ASCII.GetString(Span);
    }

    public string ToASCIIString(int start)
    {
        var data = Span.Slice(start);

        return Encoding.ASCII.GetString(data);
    }

    public string ToASCIIString(int start, int length)
    {
        var data = Span.Slice(start, length);

        return Encoding.ASCII.GetString(data);
    }

    public string ToASCIIString(int start, BucketEol eol)
    {
        return ToASCIIString(start, Length - start - eol.CharCount());
    }

    public string ToASCIIString(BucketEol eol)
    {
        return ToASCIIString(0, Length - eol.CharCount());
    }


    public string ToUTF8String()
    {
        return Encoding.UTF8.GetString(Span);
    }

    public string ToUTF8String(int start)
    {
        var data = Span.Slice(start);

        return Encoding.UTF8.GetString(data);
    }

    public string ToUTF8String(int start, int length)
    {
        var data = Span.Slice(start, length);

        return Encoding.UTF8.GetString(data);
    }

    public string ToUTF8String(int start, BucketEol eol)
    {
        return ToUTF8String(start, Length - start - eol.CharCount());
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

    public BucketBytes[] Split(byte separator)
    {
        int next = IndexOf(separator);

        if (next < 0)
            return new BucketBytes[] { this };

        int start = 0;

        var result = new List<BucketBytes>();
        while(next >= 0)
        {
            if (next > start)
                result.Add(Slice(start, next - start));

            start = next + 1;
            if (start >= Length)
                break;

            next = IndexOf(separator, start);
        }

        if (start < Length)
            result.Add(Slice(start));

        return result.ToArray();
    }

    public BucketBytes[] Split(byte separator, int count)
    {
        int next = IndexOf(separator);
        if (next < 0)
            return new BucketBytes[] { this };

        int start = 0;

        var result = new List<BucketBytes>(count);
        count--;

        while (next > 0 && result.Count < count)
        {
            result.Add(Slice(start, next - start));

            start = next + 1;
            if (start >= Length)
                break;

            next = IndexOf(separator, start);
        }

        if (start < Length)
            result.Add(Slice(start));

        return result.ToArray();
    }

    private static bool IsWhiteSpace(byte v)
    {
        return v switch
        {
            (byte)' ' or (byte)'\n' or (byte)'\r' or (byte)'\t' or (byte)'\v' => true,
            _ => false,
        };
    }

    public bool StartsWithASCII(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentNullException(nameof(value));

        var p = Span;

        if (p.Length < value.Length)
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            if (p[i] != value[i])
                return false;
        }

        return true;
    }

    public bool EndsWithASCII(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentNullException(nameof(value));
        else if (Length < value.Length)
            return false;

        var p = Memory.Slice(Length - value.Length).Span;

        for(int i = 0; i < value.Length; i++)
        {
            if (p[i] != value[i])
                return false;
        }

        return true;
    }

    public bool EqualsASCII(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentNullException(nameof(value));

        var p = Span;

        if (p.Length != value.Length)
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            if (p[i] != value[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="remaining"></param>
    /// <param name="requested"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static BucketBytes PartialReturn(ref BucketBytes remaining, int requested)
    {
        if (requested <= 0)
            throw new ArgumentOutOfRangeException(nameof(requested), requested, message: null);

        BucketBytes bb;
        if (remaining.IsEof)
            return remaining;
        else if (requested >= remaining.Length)
        {
            bb = remaining;
            remaining = BucketBytes.Empty;
        }
        else
        {
            bb = remaining.Slice(0, requested);
            remaining = remaining.Slice(requested);
        }
        return bb;
    }
}
