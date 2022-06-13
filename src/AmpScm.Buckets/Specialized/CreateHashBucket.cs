using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class CreateHashBucket : WrappingBucket, IBucketPoll
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        HashAlgorithm? _hasher;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        byte[]? _result;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
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

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
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
                {
                    try
                    {
                        _onResult?.Invoke(_result);
                    }
                    finally
                    {
                        _onResult = null;
                    }
                }
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

        public override Bucket Duplicate(bool reset = false) => Inner.Duplicate(reset);

        public override void Reset()
        {
            Inner.Reset();
            _hasher?.Initialize();
            _result = null;
        }

        protected override void InnerDispose()
        {
            try
            {
                if (_hasher != null)
                {
                    if (_result == null && _onResult != null)
                        FinishHashing();

                    _hasher.Dispose();
                }
            }
            finally
            {
                _hasher = null;
                base.InnerDispose();
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
#if NET6_0_OR_GREATER
            readonly System.IO.Hashing.Crc32 _crc = new();
            byte[] _hash = default!;

            public override void Initialize()
            {
                //throw new NotImplementedException();
            }

            public override bool CanReuseTransform => true;

            public override bool CanTransformMultipleBlocks => true;

            public override byte[]? Hash => _hash;

            protected override void HashCore(byte[] array, int ibStart, int cbSize)
            {
                _crc.Append(array.AsSpan(ibStart, cbSize));
            }

            protected override byte[] HashFinal()
            {
                byte[] result = new byte[_crc.HashLengthInBytes];

                if (_crc.TryGetHashAndReset(result, out var bw))
                {
                    return _hash = result;
                }

                throw new InvalidOperationException();
            }

            public override int HashSize => _crc.HashLengthInBytes * 8;
#else

            const uint DefaultPolynomial = 0xedb88320u;
            const uint DefaultSeed = 0xffffffffu;

            static uint[]? defaultTable;

            readonly uint[] table;
            uint hash;

            public Crc32()
            {
                table = InitializeTable(DefaultPolynomial);
                hash = DefaultSeed;
            }

            public override void Initialize()
            {
                hash = DefaultSeed;
            }

            protected override void HashCore(byte[] array, int ibStart, int cbSize)
            {
                hash = CalculateHash(table, hash, array, ibStart, cbSize);
            }

            protected override byte[] HashFinal()
            {
                var hashBuffer = BitConverter.GetBytes(~hash);
                HashValue = hashBuffer;
                return hashBuffer;
            }

            public override int HashSize => sizeof(int) * 8;

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
#endif

            public static new Crc32 Create() => new();
        }

        internal sealed class Crc24 : HashAlgorithm
        {
            const uint DefaultPolynomial = 0x1864cfb;
            const uint DefaultSeed = 0xb704ce;

            uint hash;

            public Crc24()
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
                var hashBuffer = BitConverter.GetBytes(hash);
                HashValue = hashBuffer;
                return hashBuffer;
            }

            public override int HashSize => sizeof(int) * 8;

            static uint CalculateHash(uint seed, IList<byte> buffer, int start, int size)
            {
                var hash = seed;
                for (var n = start; n < start + size; n++)
                {
                    hash ^= (uint)(buffer[n] << 16);
                    for (int i = 0; i < 8; i++)
                    {
                        hash <<= 1;
                        if (0 != (hash & 0x1000000))
                            hash ^= DefaultPolynomial;
                    }
                }

                return hash & 0xFFFFFF;
            }

            public static new Crc24 Create() => new();
        }
    }
}
