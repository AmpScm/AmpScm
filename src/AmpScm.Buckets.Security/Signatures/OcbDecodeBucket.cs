using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Signatures
{
#pragma warning disable RS0016 // Add public types and members to the declared API
    public class OcbDecodeBucket : ConversionBucket
    {
        static readonly byte[] _zero16 = new byte[16];
        private readonly Aes _aes;
        private readonly long _chunkSize;
        private readonly ReadOnlyMemory<byte> _associatedData;
        const int _blocksize = 16;
        byte[]? _buffer2;
        int _inChunkBlock;
        ByteCollector _byteCollector;
        byte[] _offset;
        byte[] _checksum;
        byte[]? _buf16;

        public OcbDecodeBucket(Bucket inner, Aes aes, long chunkSize, int tagLen, ReadOnlyMemory<byte> nonce, ReadOnlyMemory<byte> associatedData)
            : base(inner)
        {
            if (aes is null)
                throw new ArgumentNullException(nameof(aes));

            _aes = aes;
            _chunkSize = chunkSize;
            _associatedData = associatedData;
            var nnonce = new byte[16];

            nonce.CopyTo(nnonce.AsMemory(16 - nonce.Length));
            nnonce[16 - nonce.Length - 1] = 0x01; // Set lowest bit
            nnonce[0] |= (byte)(tagLen << 1); // Set highest 7 bits
            var bottom = nnonce[15] & ~0b11000000;
            nnonce[15] &= 0b11000000;

            var kTop = Encipher(nnonce);

            var stretched = kTop.Concat(ArrayXor(kTop.AsMemory(0, 8).ToArray(), kTop.AsMemory(1, 8))).ToArray();

            //Debug.WriteLine($"kTop:      {DumpData(kTop.Span)}");
            //Debug.WriteLine($"Stretched: {DumpData(stretched)}");

            _offset = GetBits(stretched, bottom, 128);

            //Debug.WriteLine($"Offset_0: {DumpData(_offset)}");

            _checksum = new byte[16];
        }

#if DEBUG
        static string DumpData(Span<byte> span)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < span.Length; i++)
            {
                sb.Append(span[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }
#endif

        public static Aes SetupAes(byte[] key)
        {
            Aes aes = Aes.Create();
            aes.Key = key.Reverse().ToArray();
            aes.IV = new byte[aes.IV.Length];
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.FeedbackSize = aes.BlockSize;

            return aes;
        }

        static void SpanXor(Span<byte> a, ReadOnlySpan<byte> b)
        {
            Debug.Assert(a.Length == b.Length);

            for (int i = 0; i < a.Length; i++)
            {
                a[i] ^= b[i];
            }
        }

        static byte[] ArrayXor(ReadOnlyMemory<byte> a, ReadOnlyMemory<byte> b)
        {
            byte[] result = a.ToArray();

            SpanXor(result, b.Span);

            return result;
        }

        static void XorPosition(Span<byte> buf, ulong pos)
        {
            unchecked
            {
                buf[0] ^= (byte)(pos >> 56);
                buf[1] ^= (byte)(pos >> 48);
                buf[2] ^= (byte)(pos >> 40);
                buf[3] ^= (byte)(pos >> 32);
                buf[4] ^= (byte)(pos >> 24);
                buf[5] ^= (byte)(pos >> 16);
                buf[6] ^= (byte)(pos >> 8);
                buf[7] ^= (byte)(pos >> 0);
            };
        }

        // If the highest bit of the 128 bit integer (encoded in network order) is 0:
        // then 2* the from value, otherwise 2* the from value ^ 0x87
        //
        // Note that this function is documented as SHOULD BE constant time, for security reasons.
        static void SpanDouble(Span<byte> data, ReadOnlySpan<byte> from)
        {
            Debug.Assert(data.Length == from.Length);

            int last = data.Length - 1;
            for (int i = 0; i < last; i++)
            {
                data[i] = (byte)(from[i] << 1 | from[i + 1] >> 7);
            }
            //Debug.WriteLine($"D: {(from[0] >> 7)}");
            data[last] = (byte)(from[last] << 1 ^ ((from[0] >> 7) * 0b10000111));
        }

        static byte[] GetBits(byte[] array, int pos, int bits)
        {
            if (pos + bits > array.Length * 8)
                throw new ArgumentOutOfRangeException(nameof(bits));

            var bytes = bits / 8;

            int nByteStart = pos / 8;
            int nBitStart = pos % 8;

            if (nBitStart == 0)
                return array.AsMemory(nByteStart, bytes).ToArray();

            byte[] result = new byte[bytes];


            for (int i = 0; i < bytes; i++)
                result[i] = (byte)(array[nByteStart + i] << nBitStart | array[nByteStart + i + 1] >> (8 - nBitStart));

            return result;
        }

        static int TrailingZeros(int n)
        {
#if NETCOREAPP
            return BitOperations.TrailingZeroCount(n);
#else
            var ntz = 0;
            for (int i = 1; (n & i) == 0; i <<= 1)
            {
                ntz++;
            }
            return ntz;
#endif
        }

        byte[] Encipher(byte[] input)
        {
#if NETCOREAPP
            return _aes.EncryptCbc(input, _zero16, PaddingMode.None);
#else
            using var ec = _aes.CreateEncryptor();
            return ec.TransformFinalBlock(input, 0, input.Length);
#endif
        }

        byte[] Decrypt(ReadOnlyMemory<byte> input)
        {
#if NETCOREAPP
            return _aes.DecryptCbc(input.Span, _zero16, PaddingMode.None);
#else
            if (!MemoryMarshal.TryGetArray(input, out var seg))
                throw new InvalidOperationException();

            using var ec = _aes.CreateEncryptor();
            return ec.TransformFinalBlock(seg.Array!, seg.Offset, seg.Count);
#endif
        }

        void Decrypt(ReadOnlyMemory<byte> src, Span<byte> dest)
        {
            var d = Decrypt(src);

            d.AsSpan().CopyTo(dest);
        }

        readonly List<byte[]> _masks = new();
        byte[] GetMask(int n)
        {
            n += 2;

            if (n >= _masks.Count)
            {
                if (_masks.Count == 0)
                {
                    // L_* = ENCCIPHER(K, zeros(128)
                    _masks.Add(Encipher(_zero16));
                }

                while (n >= _masks.Count)
                {
                    var b = new byte[16];
                    SpanDouble(b, _masks[_masks.Count - 1]); // L_i = double(L_{i-1})

                    //Debug.WriteLine($"L_{_masks.Count}: {DumpData(b)}");
                    _masks.Add(b);
                }
            }

            return _masks[n];
        }

        const int TagSize = 16;

        protected override BucketBytes ConvertData(ref BucketBytes sourceData, bool final)
        {
            // We don't want to convert the final TagSize bytes using the normal convert
            int available = _byteCollector.Length + sourceData.Length;

            return DoConvertData(ref sourceData, available - TagSize, final);
        }

        protected override async ValueTask<(BucketBytes Result, BucketBytes SourceData)> ConvertDataAsync(BucketBytes sourceData, bool final)
        {
            int available = _byteCollector.Length + sourceData.Length;

            var rem = final ? 0 : await Inner.ReadRemainingBytesAsync().ConfigureAwait(false);

            // TODO: If 'rem' = 0, then we can pass final as true and complete in one step.

            if (!(rem > 16))
                available -= 16 - (int)(rem ?? 0);

            var r = DoConvertData(ref sourceData, available, final);

            return (r, sourceData);
        }

        protected BucketBytes DoConvertData(ref BucketBytes sourceData, int available, bool final)
        {
            ReadOnlyMemory<byte> srcData;
            var src = _buf16 ??= new byte[16];

            // For the normal reads, we never want to read the final 16 bytes "TAG"
            if (available < TagSize)
            {
                _byteCollector.Append(sourceData);
                sourceData = final ? BucketBytes.Eof : BucketBytes.Empty;

                if (!final)
                    return BucketBytes.Empty;

                srcData = _byteCollector.ToArray();
                _byteCollector.Clear();

                if (available > 0)
                {
                    // We have remaining data
                    srcData.Slice(0, available).CopyTo(src);

                    // Offset_* = Offset_m xor L_*
                    SpanXor(_offset, GetMask(-2));
                    // Pad = ENCIPHER(K, Offset_*)
                    // P_ * = C_ * xor Pad[1..bitlen(C_ *)]
                    SpanXor(src, Encipher(_offset));

                    // Checksum_* = Checksum_m xor (P_* || 1 || zeros(127-bitlen(P_*)))
                    src[available] = 0x80;
                    if (available < _blocksize)
                        Array.Clear(src, available + 1, src.Length - available - 1);

                    //Debug.WriteLine($"P*: {DumpData(src.AsSpan(0, available))}");

                    SpanXor(_checksum, _buf16);


                    SpanXor(_checksum, _offset);
                    SpanXor(_checksum, GetMask(-1));

                    var tag = Encipher(_checksum);
                    SpanXor(tag, Hash(_associatedData).Span);

                    if (!tag.AsSpan().SequenceEqual(srcData.Slice(available).Span))
                        throw new BucketException("Decrypted data not valid");
                }


                return available > 0 ? new(src, 0, available) : BucketBytes.Eof;
            }

            available -= available % _blocksize;


            if (_byteCollector.Length > 0)
            {
                _byteCollector.Append(sourceData);

                srcData = _byteCollector.ToMemory();
                _byteCollector.Clear();

                _byteCollector.Append(srcData.Slice(available).ToArray());
                srcData = srcData.Slice(0, available);
            }
            else
            {
                _byteCollector.Append(sourceData.Slice(available).ToArray());
                srcData = sourceData.Slice(0, available);
            }
            sourceData = BucketBytes.Empty;

            _buffer2 ??= new byte[1024];

            int convertBlocks = available / _blocksize;

            for (int i = 0; i < convertBlocks; i++)
            {
                ++_inChunkBlock;

                //Debug.WriteLine($"trailing zeros of {_inChunkBlock} is {TrailingZeros(_inChunkBlock)}");
                //Debug.WriteLine($"L_{TrailingZeros(_inChunkBlock)} is {DumpData(GetMask(TrailingZeros(_inChunkBlock)))}");

                SpanXor(_offset, GetMask(TrailingZeros(_inChunkBlock)));

                //Debug.WriteLine($"Offset_{_inChunkBlock} = {DumpData(_offset)}");

                Array.Clear(_buf16, 0, 16);
                srcData.Slice(i * _blocksize, _blocksize).CopyTo(_buf16);
                Span<byte> dest = _buffer2.AsSpan(i * _blocksize, _blocksize);

                SpanXor(src, _offset);
                Decrypt(src, dest);
                SpanXor(dest, _offset);

                SpanXor(_checksum, dest);
            }


            //Debug.WriteLine($"P: {DumpData(_buffer2.AsSpan(0, available))}");

            return new BucketBytes(_buffer2, 0, available);
        }

        private ReadOnlyMemory<byte>Hash(ReadOnlyMemory<byte> associatedData)
        {
            var sum = new byte[16];
            var offset = new byte[16];
            var tmp = new byte[16];
            int blockNr = 1;

            while (blockNr * _blocksize < associatedData.Length)
            {
                SpanXor(offset, GetMask(blockNr));

                associatedData.Slice(blockNr * _blocksize, _blocksize).Span.CopyTo(tmp);
                SpanXor(tmp, offset);

                SpanXor(sum, Encipher(tmp));
            }

            int remaining = associatedData.Length % _blocksize;

            if (remaining > 0)
            {
                SpanXor(offset, GetMask(-2));

                Array.Clear(tmp, 0, 16);
                associatedData.Slice(associatedData.Length-remaining).CopyTo(tmp);
                tmp[remaining] = 0x80;

                SpanXor(tmp, offset);

                SpanXor(sum, Encipher(tmp));
            }
            // else sum = sim

            return sum;
        }

        protected override int ConvertRequested(int requested)
        {
            if (requested < 16)
                return 16;
            else
                return requested;
        }

        protected override ValueTask<BucketBytes> InnerReadAsync(int requested = 2146435071)
        {
            return base.InnerReadAsync(requested);
        }
    }
#pragma warning restore RS0016 // Add public types and members to the declared API
}
