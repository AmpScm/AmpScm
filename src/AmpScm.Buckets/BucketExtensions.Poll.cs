using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets;

public partial class BucketExtensions
{

    public static ValueTask<BucketBytes> PollAsync(this Bucket bucket, int minRequested = 1)
    {
        if (bucket is null)
            throw new ArgumentNullException(nameof(bucket));

        if (bucket is IBucketPoll bp)
            return bp.PollAsync(minRequested);
        else
            return new (bucket.Peek());
    }

    public static async ValueTask<BucketPollBytes> PollReadAsync(this Bucket bucket, int minRequested = 1)
    {
        if (bucket is null)
            throw new ArgumentNullException(nameof(bucket));

        BucketBytes data;
        if (bucket is IBucketPoll bucketPoll)
        {
            data = await bucketPoll.PollAsync(minRequested).ConfigureAwait(false);

            if (!data.IsEmpty || data.IsEof)
                return new BucketPollBytes(bucket, data, 0);
        }
        else
            data = bucket.Peek();

        if (data.Length >= minRequested)
            return new BucketPollBytes(bucket, data, 0); // Nice peek, move along

        data = await bucket.ReadAsync(minRequested).ConfigureAwait(false);

        if (data.IsEmpty)
            return new BucketPollBytes(bucket, BucketBytes.Eof, 0); // Nothing to optimize

        byte byte0 = data[0];
        byte[]? dataBytes = (data.Length > 0) ? data.ToArray() : null;
        int alreadyRead = data.Length;

        // Now the special trick, we might just have triggered a much longer read and in
        // that case we want to provide more data

        data = bucket.Peek();

        var (arr, offset) = data;

        if (arr is not null && offset > alreadyRead)
        {
            if ((alreadyRead == 1 && arr[offset - 1] == byte0)
                || arr.Skip(offset - alreadyRead).Take(alreadyRead).SequenceEqual(dataBytes!))
            {
                // The very lucky, but common case. The peek buffer starts with what we already read

                return new BucketPollBytes(bucket, new BucketBytes(arr, offset - alreadyRead, data.Length + alreadyRead), alreadyRead);
            }
        }

        if (data.Length > 0)
        {
            // We have original data and peeked data. Let's copy some data to help our caller
            byte[] result = new byte[alreadyRead + data.Length];

            if (alreadyRead == 1)
                result[0] = byte0;
            else
                dataBytes!.CopyTo(result, 0);

            data.CopyTo(result, alreadyRead);
            dataBytes = result;
        }
        else if (dataBytes == null)
            dataBytes = new[] { byte0 };

        return new BucketPollBytes(bucket, dataBytes, alreadyRead);
    }

    internal static string AsDebuggerDisplay(this ReadOnlyMemory<byte> Data)
    {
        if (Data.Length == 0)
            return "<Empty>";
        else
        {
            StringBuilder sb = new StringBuilder("\"");

            foreach (byte b in Data.Span)
            {
                if (b > 0 && b < 128 && !char.IsControl((char)b))
                    sb.Append((char)b);
                else switch (b)
                    {
                        case 0:
                            sb.Append("\\0");
                            break;
                        case (byte)'\n':
                            sb.Append("\\n");
                            break;
                        case (byte)'\t':
                            sb.Append("\\t");
                            break;
                        case (byte)'\r':
                            sb.Append("\\r");
                            break;
                        default:
                            sb.AppendFormat(CultureInfo.InvariantCulture, "\\x{0:X2}", b);
                            break;
                    }

                if (sb.Length > 120)
                {
                    sb.Append("...");
                    return sb.ToString();
                }
            }
            sb.Append('\"');
            return sb.ToString();
        }
    }

    internal static string AsDebuggerDisplay(this BucketBytes bb) => bb.Memory.AsDebuggerDisplay();

    internal static string AsDebuggerDisplay(this Memory<byte> bb) => AsDebuggerDisplay((ReadOnlyMemory<byte>)bb);
}
