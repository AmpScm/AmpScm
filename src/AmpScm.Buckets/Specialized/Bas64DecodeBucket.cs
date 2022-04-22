using System;
using System.Linq;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class Bas64DecodeBucket : ConvertBucket
    {
        byte[]? buffer;
        uint state;
        int n;

        public Bas64DecodeBucket(Bucket bucket)
            : base(bucket)
        {
        }

        static readonly sbyte[] base64Reversemap = CalculateReverseBase64Map();

        private static sbyte[] CalculateReverseBase64Map()
        {
            var map = Bas64EncodeBucket.base64Map.Select((x, n) => new { x, n }).ToDictionary(v => v.x, v => v.n);

            return Enumerable.Range(0, 256).Select(x =>
                (sbyte)(x switch
                {
                    '\n' => -1,
                    '\r' => -1,
                    '=' => -1,
                    _ => (map.TryGetValue((byte)x, out var v) ? v : -3)
                })).ToArray();
        }

        protected override BucketBytes ConvertData(ref BucketBytes sourceData, bool final)
        {
            buffer ??= new byte[1024];
            int i;
            int nb = 0;

            for (i = 0; i < sourceData.Length && nb + 4 < buffer.Length; i++)
            {
                byte b = sourceData[i];

                sbyte sb = base64Reversemap[b];

                if (sb < 0)
                {
                    if (sb == -1)
                        continue; // Newlines and '=' are skipped
                    else
                        throw new BucketException($"Unexpected base64 character 0x{b:x} in {Name} bucket");
                }

                state |= (uint)sb << (6 * (3 - n));

                if (n < 3)
                {
                    n++;
                    continue;
                }
                n = 0;

                for (int d = 2; d >= 0; d--)
                {
                    buffer[nb++] = (byte)((state >> 8 * d) & 0xFF);
                }
                state = 0;
            }

            if (final && n > 0)
            {
                for (int d = 2; d >= 0; d--)
                {
                    if (3 - d < n)
                        buffer[nb++] = (byte)((state >> 8 * d) & 0xFF);
                }
                n = 0;
            }

            if (i > 0) // Pass EOF through unchanged
                sourceData = sourceData.Slice(i);

            return new BucketBytes(buffer, 0, nb);
        }

        protected override int ConvertRequested(int requested)
        {
            if (requested < 1024)
                return requested * 4 / 3;

            return requested;
        }
    }
}
