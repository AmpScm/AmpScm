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

        public static Bucket GitHash(this Bucket bucket, GitIdType type, Action<GitId> created)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            switch (type)
            {
                case GitIdType.Sha1:
                    return bucket.SHA1(x => created(new GitId(type, x)));
                case GitIdType.Sha256:
                    return bucket.SHA256(x=>created(new GitId(type, x)));
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
                throw new GitBucketEofException(bucket);
        }

        public static async ValueTask<long> ReadGitOffsetAsync(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            long max_offset_len = 1 + 64 / 7;
            long delta_position = 0;

            for (int i = 0; i <= max_offset_len; i++)
            {
                var data = await bucket.ReadAsync(1).ConfigureAwait(false);

                if (data.IsEof)
                    throw new GitBucketEofException(bucket);

                byte uc = data[0];

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

        public static async ValueTask<long> ReadGitDeltaSize(this Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            long max_delta_size_len = 1 + 64 / 7;
            long size = 0;
            for (int i = 0; i < max_delta_size_len; i++)
            {
                var data = await bucket.ReadAsync(1).ConfigureAwait(false);

                if (data.IsEof)
                    throw new GitBucketEofException(bucket);

                byte uc = data[0];

                int shift = (i * 7);
                size |= (long)(uc & 0x7F) << shift;

                if (0 == (data[0] & 0x80))
                    return size;
            }

            throw new GitBucketException($"Git Delta Size overflows 64 bit integer in {bucket.Name} Bucket");
        }
    }
}
