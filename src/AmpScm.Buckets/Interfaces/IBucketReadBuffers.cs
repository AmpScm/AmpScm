namespace AmpScm.Buckets.Interfaces;

public interface IBucketReadBuffers
{
    ValueTask<(ReadOnlyMemory<byte>[] Buffers, bool Done)> ReadBuffersAsync(int requested = Bucket.MaxRead);
}
