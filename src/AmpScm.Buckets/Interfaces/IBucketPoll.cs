namespace AmpScm.Buckets.Interfaces;

public interface IBucketPoll
{
    ValueTask<BucketBytes> PollAsync(int minRequested = 1);
}
