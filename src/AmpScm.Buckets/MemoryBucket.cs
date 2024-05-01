using System.Diagnostics;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets;

[DebuggerDisplay($"{{{nameof(SafeName)},nq}}: Position={{{nameof(Position)}}}, Next={{{nameof(DebuggerDisplay)},nq}}")]
public sealed class MemoryBucket : Bucket, IBucketNoDispose, IBucketReadBuffers
{
    internal BucketBytes Data { get; }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal int Offset { get; private set; }

    public MemoryBucket(byte[] sourceData)
    {
        Data = sourceData ?? Array.Empty<byte>();
    }

    public MemoryBucket(byte[] sourceData, int start, int length)
    {
        Data = new ReadOnlyMemory<byte>(sourceData, start, length);
    }

    public MemoryBucket(ReadOnlyMemory<byte> sourceData)
    {
        Data = sourceData;
    }

    public override BucketBytes Peek()
    {
        if (Offset == Data.Length)
            return BucketBytes.Eof;

        return Data.Slice(Offset);
    }

    public override ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        int canRead = Math.Min(requested, Data.Length - Offset);

        if (canRead == 0 && requested > 0)
            return BucketBytes.Eof;

        var r = Data.Slice(Offset, canRead);
        Offset += r.Length;

        return r;
    }

    public override ValueTask<long?> ReadRemainingBytesAsync()
    {
        return new(Data.Length - Offset);
    }

    public override Bucket Duplicate(bool reset = false)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        var mb = new MemoryBucket(Data);
#pragma warning restore CA2000 // Dispose objects before losing scope
        if (!reset)
            mb.Offset = Offset;

        return mb;
    }

    public override long? Position => Offset;

    public override bool CanReset => true;

    public override void Reset()
    {
        Offset = 0;
    }

#pragma warning disable CA1033 // Interface methods should be callable by child types
    Bucket IBucketNoDispose.NoDispose()
#pragma warning restore CA1033 // Interface methods should be callable by child types
    {
        return this;
    }

    bool IBucketNoDispose.HasMultipleDisposers()
    {
        return false;
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable CA1033 // Interface methods should be callable by child types
    async ValueTask<(ReadOnlyMemory<byte>[] Buffers, bool Done)> IBucketReadBuffers.ReadBuffersAsync(int maxRequested)
#pragma warning restore CA1033 // Interface methods should be callable by child types
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        if (maxRequested >= Data.Length - Offset)
        {
            ReadOnlyMemory<byte>[] r = new[] { Data.Memory.Slice(Offset, Data.Length - Offset) };

            Offset = Data.Length;

            return (r, true);
        }
        else
        {
            ReadOnlyMemory<byte>[] r = new[] { Data.Memory.Slice(Offset, maxRequested) };

            Offset += maxRequested;

            return (r, false);
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => Data.Slice(Offset).AsDebuggerDisplay();
}
