using System.Diagnostics;
using AmpScm.Git;

namespace AmpScm.Buckets.Git;

public static class GitReadExtensions
{
    public static async ValueTask<GitId> ReadGitIdAsync(this Bucket bucket, GitIdType type)
    {
        if (bucket is null)
            throw new ArgumentNullException(nameof(bucket));

        int hl = type.HashLength();
        var bb = await bucket.ReadAtLeastAsync(hl).ConfigureAwait(false);

        return new GitId(type, bb.ToArray());
    }

    /// <summary>
    /// Reads a OFS_DELTA encoded integer from <paramref name="bucket"/>.
    /// </summary>
    /// <param name="bucket"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="BucketEofException"></exception>
    /// <exception cref="GitBucketException"></exception>
    /// <remarks>This encoding is similar, but different than <see cref="ReadGitDeltaSize(Bucket)"/>, which
    /// is also used by the delta decoding, as it encodes one additional value per additional bytes</remarks>
    public static async ValueTask<long> ReadGitDeltaOffsetAsync(this Bucket bucket)
    {
        if (bucket is null)
            throw new ArgumentNullException(nameof(bucket));

        int max_offset_len = 1 + 64 / 7;
        long delta_position = 0;

        var bb = await bucket.PollAsync(max_offset_len).ConfigureAwait(false);
        int n = bb.Span.IndexOf(x => (x & 0x80) == 0);

        if (n >= 0)
        {
            n++;
            bb = await bucket.ReadAsync(n).ConfigureAwait(false);

            if (bb.Length != n)
                throw new BucketEofException(bucket);

            for (int i = 0; i < bb.Length; i++)
            {
                byte uc = bb[i];

                if (i > 0)
                    delta_position = (delta_position + 1) << 7;

                delta_position |= (long)(uc & 0x7F);
            }

            Debug.Assert(0 == (bb[bb.Length - 1] & 0x80));
            return delta_position;
        }
        else
            for (int i = 0; i < max_offset_len; i++)
            {
                bb = await bucket.ReadAsync(1).ConfigureAwait(false);

                if (bb.IsEof)
                    throw new BucketEofException(bucket);

                byte uc = bb[0];

                if (i > 0)
                    delta_position = (delta_position + 1) << 7;

                delta_position |= (long)(uc & 0x7F);

                if (0 == (uc & 0x80))
                {
                    return delta_position;
                }
            }

        throw new GitBucketException($"Git Offset overflows 64 bit integer in {bucket.Name} Bucket");
    }

    /// <summary>
    /// Reads a 'size encoded' integer from <paramref name="bucket"/>
    /// </summary>
    /// <param name="bucket"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="BucketEofException"></exception>
    /// <exception cref="GitBucketException"></exception>
    /// <remarks>This enconding is similar, but different than <see cref="ReadGitDeltaOffsetAsync(Bucket)"/>, which
    /// is also used by the delta decoding</remarks>
    public static async ValueTask<long> ReadGitDeltaSize(this Bucket bucket)
    {
        if (bucket is null)
            throw new ArgumentNullException(nameof(bucket));

        int max_delta_size_len = 1 + 64 / 7;
        long size = 0;

        var bb = await bucket.PollAsync(max_delta_size_len).ConfigureAwait(false);
        int n = bb.Span.IndexOf(x => (x & 0x80) == 0);

        if (n >= 0)
        {
            n++;
            bb = await bucket.ReadAsync(n).ConfigureAwait(false);

            if (bb.Length != n)
                throw new BucketEofException(bucket);

            for (int i = 0; i < bb.Length; i++)
            {
                byte uc = bb[i];

                int shift = (i * 7);
                size |= (long)(uc & 0x7F) << shift;
            }

            Debug.Assert(0 == (bb[bb.Length - 1] & 0x80));
            return size;
        }
        else
            for (int i = 0; i < max_delta_size_len; i++)
            {
                bb = await bucket.ReadAsync(1).ConfigureAwait(false);

                if (bb.IsEof)
                    throw new BucketEofException(bucket);

                byte uc = bb[0];

                int shift = (i * 7);
                size |= (long)(uc & 0x7F) << shift;

                if (0 == (bb[0] & 0x80))
                    return size;
            }

        throw new GitBucketException($"Git Delta Size overflows 64 bit integer in {bucket.Name} Bucket");
    }

    internal static int IndexOf(this BucketBytes bytes, char value)
    {
        return bytes.IndexOf((byte)value);
    }

    internal static int IndexOf(this BucketBytes bytes, char value, int startOffset)
    {
        return bytes.IndexOf((byte)value, startOffset);
    }

    public static string[] SplitToUtf8String(this BucketBytes bytes, byte separator, int count)
    {
        var bbs = bytes.Split(separator, count);

        string[] s = new string[bbs.Length];

        for (int i = 0; i < bbs.Length; i++)
        {
            s[i] = bbs[i].ToUTF8String();
        }
        return s;
    }
}
