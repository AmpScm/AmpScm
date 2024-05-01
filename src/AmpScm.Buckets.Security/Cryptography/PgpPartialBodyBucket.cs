namespace AmpScm.Buckets.Cryptography;

internal sealed class PgpPartialBodyBucket : WrappingBucket
{
    private int _remaining;
    private bool _final;

    public PgpPartialBodyBucket(Bucket source, uint firstLength)
        : base(source)
    {
        _remaining = (int)firstLength;
    }

    public override BucketBytes Peek()
    {
        var b = Source.Peek();

        if (b.Length > _remaining)
        {
            return b.Slice(0, _remaining);
        }

        return b;
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        if (requested < 1)
            throw new ArgumentOutOfRangeException(nameof(requested), requested, message: null);

        if (_remaining == 0 && !_final)
        {
            var (len, partial) = await CryptoChunkBucket.ReadLengthAsync(Source).ConfigureAwait(false);

            _final = !partial;
            _remaining = (int)len!.Value;
        }

        if (_remaining > 0)
        {
            var bb = await Source.ReadAsync(Math.Min(requested, _remaining)).ConfigureAwait(false);

            _remaining -= bb.Length;
            return bb;
        }
        else
            return BucketBytes.Eof;
    }

    public override long? Position => Source.Position;

    public override ValueTask<long?> ReadRemainingBytesAsync()
    {
        if (_final)
        {
            return new(_remaining);
        }
        else
            return new((long?)null);
    }
}
