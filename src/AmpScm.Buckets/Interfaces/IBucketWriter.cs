﻿namespace AmpScm.Buckets.Interfaces;

public interface IBucketWriter
{
    void Write(Bucket bucket);

    ValueTask ShutdownAsync();
}


public interface IBucketWriterStats : IBucketWriter
{
    public long BytesWritten { get; }
}
