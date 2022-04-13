using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class CreateHashBucket : WrappingBucket, IBucketPoll
    {
        HashAlgorithm? _hasher;
        byte[]? _result;
        Action<byte[]>? _onResult;

        public CreateHashBucket(Bucket inner, HashAlgorithm hasher)
            : base(inner)
        {
            _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        }

        public CreateHashBucket(Bucket inner, HashAlgorithm hasher, Action<byte[]>? hashCreated)
            : this(inner, hasher)
        {
            _onResult = hashCreated;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            var r = await Inner.ReadAsync(requested).ConfigureAwait(false);

            if (r.IsEof)
                FinishHashing();
            else if (!r.IsEmpty)
            {
                var (bytes, offset) = r.ExpandToArray();
                _hasher?.TransformBlock(bytes, offset, r.Length, null!, 16);
            }

            return r;
        }

        void FinishHashing()
        {
            if (_result == null && _hasher != null)
            {
                _hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                _result = _hasher.Hash;
                if (_result != null)
                    _onResult?.Invoke(_result);
            }
        }

        public override BucketBytes Peek()
        {
            return Inner.Peek();
        }

        public ValueTask<BucketBytes> PollAsync(int minRequested = 1)
        {
            return Inner.PollAsync(minRequested);
        }

        public override ValueTask<long> ReadSkipAsync(long requested)
        {
            return SkipByReading(requested);
        }

        public override long? Position => Inner.Position;

        public override ValueTask<long?> ReadRemainingBytesAsync() => Inner.ReadRemainingBytesAsync();

        public override bool CanReset => Inner.CanReset && (_hasher?.CanReuseTransform ?? false);

        public override ValueTask<Bucket> DuplicateAsync(bool reset) => Inner.DuplicateAsync(reset);

        public override async ValueTask ResetAsync()
        {
            await Inner.ResetAsync().ConfigureAwait(false);
            _hasher?.Initialize();
            _result = null;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && _hasher != null)
            {
                if (_result == null && _onResult != null)
                    FinishHashing();

                _hasher.Dispose();
                _hasher = null;
            }
        }

#pragma warning disable CA1819 // Properties should not return arrays
        public byte[]? HashResult => _result;
#pragma warning restore CA1819 // Properties should not return arrays


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
            public const uint DefaultPolynomial = 0xedb88320u;
            public const uint DefaultSeed = 0xffffffffu;

            static uint[]? defaultTable;

            readonly uint seed;
            readonly uint[] table;
            uint hash;

            public Crc32()
                : this(DefaultPolynomial, DefaultSeed)
            {
            }

            public Crc32(uint polynomial, uint seed)
            {
                table = InitializeTable(polynomial);
                this.seed = hash = seed;
            }

            public override void Initialize()
            {
                hash = seed;
            }

            protected override void HashCore(byte[] array, int ibStart, int cbSize)
            {
                hash = CalculateHash(table, hash, array, ibStart, cbSize);
            }

            protected override byte[] HashFinal()
            {
                var hashBuffer = NetBitConverter.GetBytes(~hash);
                HashValue = hashBuffer;
                return hashBuffer;
            }

            public override int HashSize => sizeof(int) * 8;

            public static uint Compute(byte[] buffer)
            {
                return Compute(DefaultSeed, buffer);
            }

            public static uint Compute(uint seed, byte[] buffer)
            {
                return Compute(DefaultPolynomial, seed, buffer);
            }

            public static uint Compute(uint polynomial, uint seed, byte[] buffer)
            {
                return ~CalculateHash(InitializeTable(polynomial), seed, buffer, 0, buffer.Length);
            }

            static uint[] InitializeTable(uint polynomial)
            {
                if (polynomial == DefaultPolynomial && defaultTable != null)
                    return defaultTable;

                var createTable = new uint[256];
                for (var i = 0; i < 256; i++)
                {
                    var entry = (uint)i;
                    for (var j = 0; j < 8; j++)
                        if ((entry & 1) == 1)
                            entry = (entry >> 1) ^ polynomial;
                        else
                            entry >>= 1;
                    createTable[i] = entry;
                }

                if (polynomial == DefaultPolynomial)
                    defaultTable = createTable;

                return createTable;
            }

            static uint CalculateHash(uint[] table, uint seed, IList<byte> buffer, int start, int size)
            {
                var hash = seed;
                for (var i = start; i < start + size; i++)
                    hash = (hash >> 8) ^ table[buffer[i] ^ hash & 0xff];
                return hash;
            }

            public static new Crc32 Create() => new();
        }

        internal sealed class BytesRead : HashAlgorithm
        {
            long _len;

            public BytesRead()
            {
            }

            public override void Initialize()
            {
                _len = 0;
            }

            protected override void HashCore(byte[] array, int ibStart, int cbSize)
            {
                _len += cbSize;
            }

            protected override byte[] HashFinal()
            {
                return BitConverter.GetBytes(_len);
            }

            public override int HashSize => sizeof(long) * 8;

            public static new BytesRead Create() => new();
        }
    }
}
