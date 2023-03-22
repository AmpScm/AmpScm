using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Signatures
{
#pragma warning disable RS0016 // Add public types and members to the declared API
    public class OCBDecoder : ConversionBucket
    {
        private readonly Aes _aes;
        private readonly long _chunkSize;
        const int _blocksize = 16;
        private ICryptoTransform _dc;
        byte[]? _buffer;
        byte[]? _buffer2;
        ulong _nextBlockNr;
        long _inChunkPos;
        int _inChunkBlock;
        byte[]? _additional;
        byte[]? _16tmp;
        ByteCollector _byteCollector;
        byte[] _offset;
        byte[] _l0;
        static readonly byte[] _zero16 = new byte[16];


        public OCBDecoder(Bucket inner, Aes aes, long chunkSize, int tagLen, ReadOnlyMemory<byte> nonce)
            : base(inner)
        {
            if (aes is null)
                throw new ArgumentNullException(nameof(aes));

            _aes = aes;
            _chunkSize = chunkSize;
            var nnonce = new byte[16];

            //Aes.

            nonce.CopyTo(nnonce.AsMemory(16 - nonce.Length));
            nnonce[16 - nonce.Length - 1] = 0x01; // Set lowest bit
            nnonce[0] |= (byte)(tagLen << 1); // Set highest 7 bits
            var bottom = nnonce[15] & ~0b11000000;
            nnonce[15] &= 0b11000000;

            //aes.IV = nnonce;

            _dc = _aes.CreateDecryptor();
            _l0 = Encipher(_zero16).AsMemory(0,16).ToArray();

            var kTop = Encipher(nnonce).AsMemory(0, 16);

            var stretched = kTop.ToArray().Concat(ArrayXor(kTop.Slice(0, 8).ToArray(), kTop.Slice(1, 8))).ToArray();

            //Trace.WriteLine($"kTop:      {DumpData(kTop.Span)}");
            //Trace.WriteLine($"Stretched: {DumpData(stretched)}");

            _offset = GetBits(stretched, bottom, 128);
            //Trace.WriteLine($"Offset_0: {DumpData(_offset)}");
        }

        static string DumpData(Span<byte> span)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < span.Length; i++)
            {
                sb.Append(span[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

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
            data[last] = (byte)(from[last] ^ ((data[0] >> 7) & 0x87));
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

        ulong ntz(ulong n)
        {
            var ntz = 0ul;
            for (ulong i = 1; (n & i) == 0; i <<= 1)
            {
                ntz++;
            }
            return ntz;
        }

        byte[] Encipher(byte[] input)
        {
#if NETCOREAPP
            return _aes.EncryptCbc(input, _zero16);
#else
            using var ec = _aes.CreateEncryptor();
            return ec.TransformFinalBlock(input, 0, input.Length);
#endif
        }

        readonly List<byte[]> _masks = new();
        byte[] GetMask(int n)
        {
            if (n >= _masks.Count)
            {

                if (_masks.Count == 0)
                {
                    // L_* = ENCCIPHER(K, zeros(128)
                    var b0 = _l0.ToArray();
                    var b1 = new byte[16];
                    SpanDouble(b1, b0); // L_$ = double(L_*)
                    SpanDouble(b0, b1); // L_0 = double(L_$)
                    _masks.Add(b0);
                }

                while (n >= _masks.Count)
                {
                    var b = new byte[16];
                    SpanDouble(b, _masks[_masks.Count - 1]); // L_i = double(L_{i-1})

                    _masks.Add(b);
                }
            }

            return _masks[n];
        }


        protected override BucketBytes ConvertData(ref BucketBytes sourceData, bool final)
        {
            _buffer ??= new byte[1024];
            _buffer2 ??= new byte[1024];
            _16tmp = new byte[16];

            // TODO: Limit conversion to whatis in current chunk
            int toConvert;
            int convertBlocks;
            {
                int haveBytes = _byteCollector.Length + sourceData.Length;
                toConvert = haveBytes - haveBytes % _blocksize;

                if (toConvert == 0)
                {
                    // Can't convert anything. Store in buffer + refill
                    _byteCollector.Append(sourceData);

                    if (sourceData.IsEof)
                    {
                        if (!_byteCollector.IsEmpty)
                            throw new BucketEofException("Not enough OCB crypto data");

                        return sourceData;
                    }
                    else
                        return sourceData = BucketBytes.Empty;
                }

                convertBlocks = toConvert / _blocksize;

                if (!_byteCollector.IsEmpty)
                {
                    _byteCollector.CopyTo(_buffer);
                    int s = toConvert - _byteCollector.Length;
                    sourceData.Slice(0, s).CopyTo(_buffer.AsSpan(_byteCollector.Length));
                    _byteCollector.Clear();
                    sourceData = sourceData.Slice(s);
                }
                else
                {
                    sourceData.Slice(0, toConvert).CopyTo(_buffer);
                    sourceData = sourceData.Slice(toConvert);

                }
            }

            for (int i = 0; i < convertBlocks; i++)
            {
                SpanXor(_offset, GetMask(i + 1));

                SpanXor(_buffer.AsSpan(i * _blocksize, 16), _offset);

                _dc.TransformBlock(_buffer, i * _blocksize, 16, _buffer2, i * _blocksize);

                SpanXor(_buffer2.AsSpan(i * _blocksize, 16), _offset);
            }

            _inChunkBlock += convertBlocks;
            _inChunkPos += toConvert;

            return new BucketBytes(_buffer2, 0, toConvert);
        }

        protected override int ConvertRequested(int requested)
        {
            int needForRead = _blocksize - _byteCollector.Length;

            if (requested < needForRead)
                return needForRead;
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
