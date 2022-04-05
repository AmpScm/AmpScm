using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets
{
    partial class BucketExtensions
    {

        public static ValueTask<BucketBytes> PollAsync(this Bucket bucket, int minRequested = 1)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            if (bucket is IBucketPoll bp)
                return bp.PollAsync(minRequested);
            else
                return new ValueTask<BucketBytes>(bucket.Peek());
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
                    // The very lucky, but common case. The peek buffer starts with what we read

                    return new BucketPollBytes(bucket, new BucketBytes(arr, offset - alreadyRead, data.Length + alreadyRead), alreadyRead);
                }
            }

            if (data.Length > 0)
            {
                // We have original data and peeked data. Let's copy some data to help our caller
                byte[] result = new byte[alreadyRead + Math.Min(data.Length, 256)];

                if (alreadyRead == 1)
                    result[0] = byte0;
                else
                    Array.Copy(dataBytes!, result, alreadyRead);

                for (int i = alreadyRead; i < result.Length; i++)
                {
                    result[i] = data[i - alreadyRead];
                }
                dataBytes = result;
            }
            else if (dataBytes == null)
                dataBytes = new[] { byte0 };

            return new BucketPollBytes(bucket, dataBytes, alreadyRead);
        }
    }
}
