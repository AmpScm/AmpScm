using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed class BucketPollBytes : IDisposable
    {
        Bucket Bucket { get; }
        public BucketBytes Data { get; private set; }
        public int AlreadyRead { get; private set; }

        public long? Position => Bucket.Position - AlreadyRead;

        public int Length => Data.Length;

        public BucketPollBytes(Bucket bucket, BucketBytes data, int alreadyRead)
        {
            Bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
            Data = data;
            AlreadyRead = alreadyRead;
        }

        public async ValueTask Consume(int readBytes)
        {
            if (readBytes < AlreadyRead)
                throw new InvalidOperationException();
            if (AlreadyRead > 0)
            {
                int consume = Math.Min(readBytes, AlreadyRead);
                AlreadyRead -= consume;
                readBytes -= consume;
            }

            while (readBytes > 0)
            {
                var r = await Bucket.ReadAsync(readBytes).ConfigureAwait(false);

                if (r.IsEmpty)
                    throw new BucketException("EOF during poll consume");

                readBytes -= r.Length;
            }
        }

        public async ValueTask<BucketBytes> ReadAsync(int readBytes)
        {
            try
            {
                if (AlreadyRead == 0)
                {
                    return await Bucket.ReadAsync(readBytes).ConfigureAwait(false);
                }
                else if (readBytes <= AlreadyRead)
                {
                    if (readBytes < AlreadyRead)
                        throw new InvalidOperationException();

                    AlreadyRead = 0;
                    var r = Data.Slice(0, readBytes);
                    Data = Data.Slice(readBytes);
                    return r;
                }
                else if (readBytes > Data.Length)
                {
                    byte[] returnData;

                    if (readBytes < Data.Length)
                        returnData = Data.Slice(0, readBytes).ToArray();
                    else
                        returnData = Data.ToArray();

                    int consume = readBytes - AlreadyRead;
                    int copy = AlreadyRead;
                    AlreadyRead = 0; // No errors in Dispose please

                    var bb = await Bucket.ReadAsync(consume).ConfigureAwait(false);

                    if (bb.IsEof)
                        return new BucketBytes(returnData, 0, copy);

                    if (copy + bb.Length <= returnData.Length)
                        return new BucketBytes(returnData, 0, copy + bb.Length); // Data already available from peek buffer

                    // We got new and old data, but how can we return that?
                    var (arr, offset) = bb;

                    // Unlikely, but cheap: The return buffer is what we need
                    if (arr is not null
                        && offset >= copy
                        && new ReadOnlySpan<byte>(arr, offset - copy, copy).SequenceEqual(returnData))
                    {
                        return new BucketBytes(arr, offset - copy, bb.Length + copy);
                    }

                    byte[] ret = new byte[bb.Length + copy];

                    Array.Copy(returnData, ret, copy);
                    bb.Span.CopyTo(new Span<byte>(ret, copy, bb.Length));

                    return ret;
                }
                else
                {
                    int consume = readBytes - AlreadyRead;

                    BucketBytes slicedDataCopy = Data.Slice(0, Math.Min(readBytes, Data.Length)).ToArray();

                    var bb = await Bucket.ReadAsync(consume).ConfigureAwait(false);

                    AlreadyRead = Math.Max(0, AlreadyRead - bb.Length);

                    if (bb.Length == consume)
                        return slicedDataCopy;
                    else if (bb.Length < consume)
                        return slicedDataCopy.Slice(0, slicedDataCopy.Length - (consume - bb.Length));
                    else
                        throw new InvalidOperationException();
                }
            }
            finally
            {
                Data = BucketBytes.Empty;
            }
        }

        public void Dispose()
        {
            if (AlreadyRead > 0)
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
                throw new BucketException($"{AlreadyRead} polled bytes were not consumed");
#pragma warning restore CA1065 // Do not raise exceptions in unexpected locations
        }

        public ReadOnlySpan<byte> Span => Data.Span;


        public byte this[int index] => Data[index];

        public bool IsEmpty => Data.IsEmpty;
        public bool IsEof => Data.IsEof;

        string DebuggerDisplay
        {
            get
            {
                if (IsEof)
                    return "<EOF>";
                else
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat(CultureInfo.InvariantCulture, "Length={0}-{1}, Data=\"", Length, AlreadyRead);

                    foreach (var b in Span)
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
        }
    }    
}
