using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git.Objects
{
    public record GitTreeElementRecord
    {
        public GitTreeElementType Type { get; init; }
        public string Name { get; init; } = default!;
        public GitId Id { get; init; } = default!;

        public Bucket AsBucket()
        {
            var pf = $"{Convert.ToString((int)Type, 8)} {Name}\0";

            return new AggregateBucket(
                Encoding.UTF8.GetBytes(pf).AsBucket(),  // "100644 MyFile\0"
                Id.Hash.AsBucket());                    // Hashcode

        }
    }

    public sealed class GitTreeReadBucket : GitBucket
    {
        readonly GitIdType _idType;
        bool _checkedType;

        public GitTreeReadBucket(Bucket inner, GitIdType idType) : base(inner)
        {
            _idType = idType;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            while (await ReadTreeElementRecord().ConfigureAwait(false) != null)
            {

            }

            return BucketBytes.Eof;
        }

        public async ValueTask<GitTreeElementRecord?> ReadTreeElementRecord()
        {
            if (!_checkedType && Inner is GitObjectBucket gobb)
            {
                await gobb.ReadTypeAsync().ConfigureAwait(false);

                if (gobb.Type != GitObjectType.Tree)
                    throw new GitBucketException($"Bucket {gobb.Name} is not Tree but {gobb.Type}");

                _checkedType = true;
            }

            var (bb, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.Zero, null).ConfigureAwait(false);

            if (bb.IsEof)
                return null;

            if (eol != BucketEol.Zero)
                throw new GitBucketException("Truncated tree");

            int nSep = bb.IndexOf((byte)' ');
            if (nSep < 0)
                throw new GitBucketException("Truncated tree. No mask separator");

            string name = bb.ToUTF8String(nSep + 1, bb.Length - nSep - 1, eol);
            string mask = bb.ToASCIIString(0, nSep);

            bb = await Inner.ReadFullAsync(GitId.HashLength(_idType)).ConfigureAwait(false);

            if (nSep < 0)
                throw new GitBucketException("Truncated tree. Incomplete hash");

            var id = new GitId(_idType, bb.ToArray());

            var val = Convert.ToInt32(mask, 8);

            return new GitTreeElementRecord { Name = name, Type = (GitTreeElementType)val, Id = id };
        }
    }
}
