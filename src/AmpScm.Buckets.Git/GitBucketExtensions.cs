using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;

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
    }
}
