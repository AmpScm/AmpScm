using System.Diagnostics;
using System.Security.Cryptography;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized;

internal sealed class CreateHashBucket : WrappingBucket, IBucketPoll, IBucketProduceHash
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private HashAlgorithm? _hasher;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Action<Func<byte[]?, byte[]>>? _onResult;

    private CreateHashBucket(Bucket source, HashAlgorithm hasher)
        : base(source)
    {
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
    }

    public CreateHashBucket(Bucket source, HashAlgorithm hasher, Action<byte[]> hashCreated)
        : this(source, hasher, x => hashCreated(x(arg: null)))
    {
    }

    public CreateHashBucket(Bucket source, HashAlgorithm hasher, Action<Func<byte[]?, byte[]>> completer)
        : this(source, hasher)
    {
        _onResult = completer;
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        var r = await Source.ReadAsync(requested).ConfigureAwait(false);

        if (r.IsEof)
            FinishHashing();
        else if (!r.IsEmpty)
        {
            var (bytes, offset) = r.ExpandToArray();
            _hasher?.TransformBlock(bytes, offset, r.Length, null!, 0);
        }
        else
            Debug.Assert(false, "Reading empty");

        return r;
    }

    private void FinishHashing()
    {
        if (_hasher != null && _onResult is { } onResult)
        {
            _onResult = null;
            onResult(CompleteHandler);
        }
    }

    private byte[] CompleteHandler(byte[]? suffix)
    {
        if (_hasher != null)
        {
            using var h = _hasher;
            _hasher = null;

            h.TransformFinalBlock(suffix ?? Array.Empty<byte>(), 0, suffix?.Length ?? 0);
            return h.Hash ?? throw new InvalidOperationException();
        }
        else
            return null!;
    }

    public override BucketBytes Peek()
    {
        return Source.Peek();
    }

    public ValueTask<BucketBytes> PollAsync(int minRequested = 1)
    {
        return Source.PollAsync(minRequested);
    }

    public override ValueTask<long> ReadSkipAsync(long requested)
    {
        return SkipByReading(requested);
    }

    public override long? Position => Source.Position;

    public override ValueTask<long?> ReadRemainingBytesAsync() => Source.ReadRemainingBytesAsync();

    public override bool CanReset => Source.CanReset && (_hasher?.CanReuseTransform ?? false);

    public override Bucket Duplicate(bool reset = false) => Source.Duplicate(reset);

    public override void Reset()
    {
        Source.Reset();
        _hasher?.Initialize();
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        try
        {
            if (disposing && _hasher != null)
            {
                if (_onResult != null)
                    FinishHashing();

                _hasher?.Dispose();
            }
        }
        finally
        {
            _hasher = null;
            await base.DisposeAsync(disposing);
        }
    }

    void IBucketProduceHash.ProduceHash()
    {
        try
        {
            if (_hasher != null)
            {
                if (_onResult != null)
                    FinishHashing();

                _hasher?.Dispose();
            }
        }
        finally
        {
            _hasher = null;
        }
    }

    // From https://github.com/damieng/DamienGKit/blob/master/CSharp/DamienG.Library/Security/Cryptography/Crc32.cs
    /// <summary>
    /// Implements a 32-bit CRC hash algorithm compatible with Zip etc.
    /// </summary>
    /// <remarks>
    /// Crc32 should only be used for backward compatibility with older file formats
    /// and algorithms. It is not secure enough for new applications.
    /// If you need to call multiple times for the same data either use the HashAlgorithm
    /// interface or remember that the result of one Compute call needs to be ~ (XOR) before
    /// being passed in as the seed for the next Compute call.
    /// </remarks>
    internal sealed class Crc32 : HashAlgorithm
    {
        private const uint DefaultPolynomial = 0xedb88320u;
        private const uint DefaultSeed = 0xffffffffu;
        private static readonly uint[] _table = InitializeTable();
        private uint hash;

        private Crc32()
        {
            hash = DefaultSeed;
        }

        public override void Initialize()
        {
            hash = DefaultSeed;
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            hash = CalculateHash(hash, array, ibStart, cbSize);
        }

        protected override byte[] HashFinal()
        {
            byte[] hashBuffer = BitConverter.GetBytes(~hash);
            HashValue = hashBuffer;
            return hashBuffer;
        }

        public override int HashSize => sizeof(int) * 8;

        private static uint[] InitializeTable()
        {
            uint[] createTable = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                uint entry = (uint)i;
                for (int j = 0; j < 8; j++)
                    if ((entry & 1) == 1)
                        entry = (entry >> 1) ^ DefaultPolynomial;
                    else
                        entry >>= 1;
                createTable[i] = entry;
            }

            return createTable;
        }

        private static uint CalculateHash(uint seed, Span<byte> buffer, int start, int size)
        {
            uint hash32 = seed;
            for (int i = start; i < start + size; i++)
                hash32 = (hash32 >> 8) ^ _table[buffer[i] ^ hash32 & 0xff];
            return hash32;
        }

        public static new Crc32 Create() => new();
    }

    /// <summary>
    /// CRC24, as documented in RFC 4880
    /// </summary>
    internal sealed class Crc24 : HashAlgorithm
    {
        private const uint DefaultPolynomial = 0x1864cfb;
        private const uint DefaultSeed = 0xb704ce;
        private uint hash;

        private Crc24()
        {
            hash = DefaultSeed;
        }

        public override void Initialize()
        {
            hash = DefaultSeed;
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            hash = CalculateHash(hash, array, ibStart, cbSize);
        }

        protected override byte[] HashFinal()
        {
            byte[] hashBuffer = BitConverter.GetBytes(hash);
            HashValue = hashBuffer;
            return hashBuffer;
        }

        public override int HashSize => 3 * 8;

        private static uint CalculateHash(uint seed, Span<byte> buffer, int start, int size)
        {
            uint hash24 = seed;
            for (int n = start; n < start + size; n++)
            {
                hash24 ^= (uint)(buffer[n] << 16);
                for (int i = 0; i < 8; i++)
                {
                    hash24 <<= 1;
                    if (0 != (hash24 & 0x1000000))
                        hash24 ^= DefaultPolynomial;
                }
            }

            return hash24 & 0xFFFFFF;
        }

        public static new Crc24 Create() => new();
    }
}
