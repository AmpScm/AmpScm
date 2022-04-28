using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Git.Objects;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Git
{
    public static class GitBucketExtensions
    {
        /// <summary>
        /// Return length of this hash in bytes
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static int HashLength(this GitIdType type)
        {
            return GitId.HashLength(type);
        }

        /// <summary>
        /// Returns true if the element represents a file on disk, otherwise false
        /// </summary>
        /// <param name="elementType"></param>
        /// <returns></returns>
        public static bool IsFile(this GitTreeElementType elementType)
            => elementType switch
            {
                GitTreeElementType.File => true,
                GitTreeElementType.FileExecutable => true,
                GitTreeElementType.SymbolicLink => true,
                _ => false,
            };

        /// <summary>
        /// Returns true if the element represents a directory on disk, otherwise false
        /// </summary>
        /// <param name="elementType"></param>
        /// <returns></returns>
        public static bool IsDirectory(this GitTreeElementType elementType)
            => elementType switch
            {
                GitTreeElementType.Directory => true,
                GitTreeElementType.GitCommitLink => true,
                _ => false,
            };

        public static Bucket CreateHeader(this GitObjectType type, long length)
        {
            string txt;
            switch (type)
            {
                case GitObjectType.Blob:
                    txt = $"blob {length}\0";
                    break;
                case GitObjectType.Tree:
                    txt = $"tree {length}\0";
                    break;
                case GitObjectType.Commit:
                    txt = $"commit {length}\0";
                    break;
                case GitObjectType.Tag:
                    txt = $"tag {length}\0";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }

            return Encoding.ASCII.GetBytes(txt).AsBucket();
        }

        public static BucketBytes Slice(this BucketBytes bb, int start, BucketEol untilEol)
        {
            return bb.Slice(start, bb.Length - start - untilEol.CharCount());
        }

        public static BucketBytes Slice(this BucketBytes bb, BucketEol untilEol)
        {
            return bb.Slice(0, bb.Length - untilEol.CharCount());
        }

        public static Bucket GitHash(this Bucket bucket, GitIdType type, Action<byte[]> created)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            switch (type)
            {
                case GitIdType.Sha1:
                    return bucket.SHA1(created);
                case GitIdType.Sha256:
                    return bucket.SHA256(created);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public static async ValueTask<GitId> ReadGitIdAsync(this Bucket bucket, GitIdType type)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            int hl = type.HashLength();
            var bb = await bucket.ReadFullAsync(hl).ConfigureAwait(false);

            if (bb.Length == hl)
                return new GitId(type, bb.ToArray());
            else
                throw new GitBucketException($"Unexpected EOF while reading GitId from {bucket.Name} Bucket");
        }

        public static async ValueTask<long> ReadGitOffsetAsync(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            long max_delta_size_len = 1 + (64 + 6) / 7;

            var polled = await bucket.PollAsync().ConfigureAwait(false);
            int rq_len;

            if (!polled.IsEmpty)
            {
                rq_len = 1;
                for (int i = 0; i < polled.Length; i++)
                {
                    if (0 == (polled[i] & 0x80))
                        break;
                    rq_len++;
                }
                Debug.Assert(rq_len <= polled.Length + 1);
            }
            else
                rq_len = 1;

            if (rq_len > max_delta_size_len)
                throw new GitBucketException($"GitOffset overflows 64 bit integer in {bucket.Name} Bucket");

            var read = await bucket.ReadAsync(rq_len).ConfigureAwait(false);
            int position = 0;
            long delta_position = 0;

            for (int i = 0; i < read.Length; i++)
            {
                byte uc = read[i];

                if (position > 0)
                    delta_position = (delta_position + 1) << 7;

                delta_position |= (long)(uc & 0x7F);
                position++;

                if (position > max_delta_size_len)
                    throw new GitBucketException($"GitOffset overflows 64 bit integer in {bucket.Name} Bucket");

                if (0 == (uc & 0x80))
                {
                    Debug.Assert(i == read.Length - 1);
                    return delta_position;
                }
                else if (i == read.Length -1)
                {
                    // Not enough data read yet. Read another byte and continue :(
                    read = await bucket.ReadAsync(1).ConfigureAwait(false);
                    i = -1;
                }
            }

            throw new BucketException($"Invalid GitOffset in {bucket.Name} Bucket");
        }
    }
}
