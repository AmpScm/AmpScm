namespace AmpScm.Buckets.Git;

public class GitPacketBucket : GitBucket
{
    private int _packetLength;

    public GitPacketBucket(Bucket source) : base(source)
    {
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        while (!(await ReadFullPacket().ConfigureAwait(false)).IsEof)
        {

        }
        while (await ReadSkipAsync(MaxRead).ConfigureAwait(false) > 0)
        {

        }

        return BucketBytes.Eof;
    }


    public int CurrentPacketLength => _packetLength;

    public async ValueTask<BucketBytes> ReadFullPacket()
    {
        BucketBytes bb = await Source.ReadAtLeastAsync(4, throwOnEndOfStream: false).ConfigureAwait(false);

        if (bb.IsEof)
            return bb;
        else if (bb.Length < 4)
            throw new BucketEofException(this);

        _packetLength = Convert.ToInt32(bb.ToASCIIString(), 16);

        if (_packetLength <= 4)
            return BucketBytes.Empty;

        bb = await Source.ReadAtLeastAsync(_packetLength - 4, throwOnEndOfStream: false).ConfigureAwait(false);

        if (bb.Length == _packetLength - 4)
            return bb;
        else
            throw new BucketEofException(Source);
    }
}
