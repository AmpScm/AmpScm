namespace AmpScm.Buckets.Specialized
{
    internal sealed class Bas64EncodeBucket : ConvertBucket
    {
        readonly bool _wrapLines;
        readonly bool _addPadding;
        byte[]? buffer;
        uint state;
        int n;
        int nl;


        public Bas64EncodeBucket(Bucket bucket, bool wrapLines, bool addPadding)
            : base(bucket)
        {
            _wrapLines = wrapLines;
            _addPadding = addPadding;
        }

        protected override int ConvertRequested(int requested)
        {
            if (requested > 1024)
                return requested;
            else
                return (requested * 4) / 3;
        }


        internal static readonly byte[] base64Map =
        {
            (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E',
            (byte)'F', (byte)'G', (byte)'H', (byte)'I', (byte)'J',
            (byte)'K', (byte)'L', (byte)'M', (byte)'N', (byte)'O',
            (byte)'P', (byte)'Q', (byte)'R', (byte)'S', (byte)'T',
            (byte)'U', (byte)'V', (byte)'W', (byte)'X', (byte)'Y',
            (byte)'Z', (byte)'a', (byte)'b', (byte)'c', (byte)'d',
            (byte)'e', (byte)'f', (byte)'g', (byte)'h', (byte)'i',
            (byte)'j', (byte)'k', (byte)'l', (byte)'m', (byte)'n',
            (byte)'o', (byte)'p', (byte)'q', (byte)'r', (byte)'s',
            (byte)'t', (byte)'u', (byte)'v', (byte)'w', (byte)'x',
            (byte)'y', (byte)'z', (byte)'0', (byte)'1', (byte)'2',
            (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7',
            (byte)'8', (byte)'9', (byte)'+', (byte)'/'
        };

        protected override BucketBytes ConvertData(ref BucketBytes sourceData, bool final)
        {
            buffer ??= new byte[1024];
            int i;
            int nb = 0;

            for (i = 0; i < sourceData.Length && nb + 5 < buffer.Length; i++)
            {
                byte b = sourceData[i];

                state |= (uint)b << (8 * (2 - n));

                if (n < 2)
                {
                    n++;
                    continue;
                }
                n = 0;

                for (int d = 3; d >= 0; d--)
                {
                    buffer[nb++] = base64Map[(state >> 6 * d) & 0x3F];
                }
                nl += 1;

                if (_wrapLines && nl >= 19)
                {
                    buffer[nb++] = (byte)'\r';
                    buffer[nb++] = (byte)'\n';
                    nl = 0;
                }
                state = 0;
            }

            if (final && n > 0)
            {
                for (int d = 3; d >= 0; d--)
                {
                    if (3 - d <= n)
                        buffer[nb++] = base64Map[(state >> 6 * d) & 0x3F];
                    else if (_addPadding)
                        buffer[nb++] = (byte)'=';
                }
                n = 0;
            }

            if (i > 0) // Pass EOF through unchanged
                sourceData = sourceData.Slice(i);

            return new BucketBytes(buffer, 0, nb);
        }
    }
}
