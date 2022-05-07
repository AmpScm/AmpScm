﻿using System;
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
                    return bucket.SHA256(x => created(new GitId(type, x)));
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

#if NETFRAMEWORK
        internal static string Replace(this string? on, string oldValue, string newValue, StringComparison comparison)
        {
            if (on is null)
                throw new ArgumentNullException(nameof(on));
            if (comparison != StringComparison.Ordinal)
                throw new ArgumentOutOfRangeException(nameof(comparison));
            return on.Replace(oldValue, newValue);
        }

        internal static int IndexOf(this string on, char value, StringComparison comparison)
        {
            if (comparison != StringComparison.Ordinal)
                throw new ArgumentOutOfRangeException(nameof(comparison));

            return on.IndexOf(value);
        }

        internal static bool Contains(this string on, char value, StringComparison comparison)
        {
            if (comparison != StringComparison.Ordinal)
                throw new ArgumentOutOfRangeException(nameof(comparison));

            return on.Contains(value);
        }

        internal static bool Contains(this string on, string value, StringComparison comparison)
        {
            if (comparison != StringComparison.Ordinal)
                throw new ArgumentOutOfRangeException(nameof(comparison));

            return on.Contains(value);
        }

        internal static int GetHashCode(this string on, StringComparison comparison)
        {
            if (comparison != StringComparison.Ordinal)
                throw new ArgumentOutOfRangeException(nameof(comparison));

            return on.GetHashCode();
        }
#endif
    }
}
