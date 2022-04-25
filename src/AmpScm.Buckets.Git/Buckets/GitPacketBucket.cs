using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            while (await ReadSkipAsync(int.MaxValue).ConfigureAwait(false) > 0)
            {

            }

            return BucketBytes.Eof;
        }


        public int CurrentPacketLength => _packetLength;

        public async ValueTask<BucketBytes> ReadFullPacket()
        {
            byte[]? start;

            using var poll = await Inner.PollReadAsync(4).ConfigureAwait(false);
            if (poll.Length < 4)
            {
                var pb = await poll.ReadAsync(4).ConfigureAwait(false);

                if (pb.IsEof)
                    return BucketBytes.Eof;

                start = pb.ToArray();

                while (start.Length < 4)
                {
                    pb = await Inner.ReadAsync(4 - start.Length).ConfigureAwait(false);

                    if (pb.IsEof)
                        throw new GitBucketException($"Unexpected EOF in packet header of {Name} bucket");

                    start = start.Concat(pb.ToArray()).ToArray();
                }
            }
            else
            {
                start = (await poll.ReadAsync(4).ConfigureAwait(false)).ToArray();
            }

            _packetLength = Convert.ToInt32(Encoding.ASCII.GetString(start), 16);

            if (_packetLength <= 4)
                return BucketBytes.Empty;

            BucketBytes bb = await Inner.ReadAsync(_packetLength - 4).ConfigureAwait(false);

            if (bb.Length == _packetLength)
                return bb;
            else if (bb.IsEof)
                throw new GitBucketException($"Unexpected EOF in packed in {Name} bucket");

            byte[] fullPacked = new byte[_packetLength - 4];
            bb.CopyTo(fullPacked);
            int c = bb.Length;

            while (c < _packetLength - 4)
            {
                bb = await Inner.ReadAsync(_packetLength - c - 4).ConfigureAwait(false);

                if (bb.IsEof)
                    throw new GitBucketException($"Unexpected EOF in {Name} Bucket");

                bb.CopyTo(fullPacked.AsMemory(c));
                c += bb.Length;
            }

            return fullPacked;
        }
    }
}
