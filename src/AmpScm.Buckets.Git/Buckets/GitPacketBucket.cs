using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Git
{
    public class GitPacketBucket : GitBucket
    {
        int _packetLength;

        public GitPacketBucket(Bucket inner) : base(inner)
        {
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            while(!(await ReadFullPacket().ConfigureAwait(false)).IsEof)
            {

            }
            while (await ReadSkipAsync(int.MaxValue).ConfigureAwait(false) > 0)
            {

            }

            return BucketBytes.Eof;
        }


        public int CurrentPacketLength => _packetLength;

        public async ValueTask<BucketBytes> ReadFullPacket()
        {
            BucketBytes bb = await Inner.ReadFullAsync(4).ConfigureAwait(false);

            if (bb.IsEof)
                return bb;
            else if (bb.Length < 4)
                GitBucketEofException.Throw(this);

            _packetLength = Convert.ToInt32(bb.ToASCIIString(), 16);

            if (_packetLength <= 4)
                return BucketBytes.Empty;

            bb = await Inner.ReadFullAsync(_packetLength - 4).ConfigureAwait(false);

            if (bb.Length == _packetLength-4)
                return bb;
            else
                throw new GitBucketEofException($"Unexpected EOF in packed in {Name} bucket");
        }
    }
}
