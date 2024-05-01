namespace AmpScm.Buckets.Interfaces;

public interface IBucketSeek
{
    ValueTask SeekAsync(long newPosition);
}

public interface IBucketSeekOnReset
{
    Bucket SeekOnReset();
}

public interface IBucketDuplicateSeekedAsync : IBucketSeek
{
    ValueTask<Bucket> DuplicateSeekedAsync(long newPosition);
}
