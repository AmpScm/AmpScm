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
using AmpScm.Buckets.Cryptography;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Cryptography;

public class OcbDecodeBucket : ConversionBucket
{
    private static readonly byte[] _zero16 = new byte[16];
    private readonly Aes _aes;
    private readonly ReadOnlyMemory<byte> _associatedData;
    private byte[]? _buffer2;
    private int _inChunkBlock;
    private ByteCollector _byteCollector;
    private readonly byte[] _offset;
    private readonly byte[] _checksum;
    private byte[]? _buf16;
    private readonly Action<bool>? _verified;


    public const int MaxNonceLength = 15;
    public const int BlockLength = 16;

    /// <summary>
    /// Length of the tag in bytes
    /// </summary>
    protected int TagSize { get; }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="inner"></param>
    /// <param name="aesKey"></param>
    /// <param name="tagLen">Length of the tag in bits</param>
    /// <param name="nonce"></param>
    /// <param name="associatedData"></param>
    /// <param name="verifyResult"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public OcbDecodeBucket(Bucket inner, byte[] aesKey, int tagLen, ReadOnlyMemory<byte> nonce, ReadOnlyMemory<byte> associatedData = default, Action<bool>? verifyResult = null)
        : base(inner)
    {
        if (tagLen < 8 || tagLen > BlockLength * 8 || tagLen % 8 != 0)
            throw new ArgumentOutOfRangeException(nameof(tagLen), tagLen, message: null);
        else if (nonce.Length > MaxNonceLength)
            throw new ArgumentOutOfRangeException(nameof(nonce));

        Debug.Assert(tagLen == 128);

        _aes = Aes.Create();
        _aes.Key = aesKey.ToArray();
        _aes.IV = new byte[_aes.IV.Length];
        _aes.Mode = CipherMode.CBC;
        _aes.Padding = PaddingMode.None;
        _aes.FeedbackSize = _aes.BlockSize;

        // We use the public 15 byte nonce to construct an internal 16 byte nonce, see RFC2753
        byte[] nnonce = new byte[16];

        nonce.CopyTo(nnonce.AsMemory(16 - nonce.Length));
        nnonce[16 - nonce.Length - 1] = 0x01; // Set lowest bit
        nnonce[0] |= (byte)(tagLen << 1); // Set highest 7 bits
        int bottom = nnonce[15] & ~0b11000000;
        nnonce[15] &= 0b11000000;

        byte[] kTop = Encipher(nnonce);

        byte[] stretched = kTop.Concat(ArrayXor(kTop.AsMemory(0, 8).ToArray(), kTop.AsMemory(1, 8))).ToArray();

        //Debug.WriteLine($"kTop:      {DumpData(kTop.Span)}");
        //Debug.WriteLine($"Stretched: {DumpData(stretched)}");

        _offset = GetBits(stretched, bottom, 128);

        //Debug.WriteLine($"Offset_0: {DumpData(_offset)}");

        _checksum = new byte[16];
        TagSize = tagLen / 8;
        _verified = verifyResult;
        _associatedData = associatedData;
    }

    protected override void InnerDispose()
    {
        try
        {
            _aes.Dispose();
        }
        finally
        {
            base.InnerDispose();
        }
    }

#if DEBUG
    private static string DumpData(Span<byte> span)
    {
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < span.Length; i++)
        {
            sb.Append(span[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }
#endif

    internal static void SpanXor(Span<byte> a, ReadOnlySpan<byte> b)
    {
        Debug.Assert(a.Length == b.Length);

        for (int i = 0; i < a.Length; i++)
        {
            a[i] ^= b[i];
        }
    }

    private static byte[] ArrayXor(ReadOnlyMemory<byte> a, ReadOnlyMemory<byte> b)
    {
        byte[] result = a.ToArray();

        SpanXor(result, b.Span);

        return result;
    }

    private static void XorPosition(Span<byte> buf, ulong pos)
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
    private static void SpanDouble(Span<byte> data, ReadOnlySpan<byte> from)
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

    private static byte[] GetBits(byte[] array, int pos, int bits)
    {
        if (pos + bits > array.Length * 8)
            throw new ArgumentOutOfRangeException(nameof(bits));

        int bytes = bits / 8;

        int nByteStart = pos / 8;
        int nBitStart = pos % 8;

        if (nBitStart == 0)
            return array.AsMemory(nByteStart, bytes).ToArray();

        byte[] result = new byte[bytes];


        for (int i = 0; i < bytes; i++)
            result[i] = (byte)(array[nByteStart + i] << nBitStart | array[nByteStart + i + 1] >> (8 - nBitStart));

        return result;
    }

    private static int TrailingZeros(int n)
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

    private byte[] Encipher(ReadOnlyMemory<byte> input)
    {
#if NETCOREAPP
        return _aes.EncryptCbc(input.Span, _zero16, PaddingMode.None);
#else
        if (!MemoryMarshal.TryGetArray(input, out var seg))
            throw new InvalidOperationException();

        using var ec = _aes.CreateEncryptor();
        return ec.TransformFinalBlock(seg.Array!, seg.Offset, seg.Count);
#endif
    }

    private byte[] Decrypt(ReadOnlyMemory<byte> input)
    {
#if NETCOREAPP
        return _aes.DecryptCbc(input.Span, _zero16, PaddingMode.None);
#else
        if (!MemoryMarshal.TryGetArray(input, out var seg))
            throw new InvalidOperationException();

        using var ec = _aes.CreateDecryptor();
        return ec.TransformFinalBlock(seg.Array!, seg.Offset, seg.Count);
#endif
    }

    private void Decrypt(ReadOnlyMemory<byte> src, Span<byte> dest)
    {
        byte[] d = Decrypt(src);

        d.AsSpan().CopyTo(dest);
    }

    private readonly List<byte[]> _masks = new();

    private byte[] GetMask(int n)
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
                byte[] b = new byte[16];
                SpanDouble(b, _masks[_masks.Count - 1]); // L_i = double(L_{i-1})

                //Debug.WriteLine($"L_{_masks.Count}: {DumpData(b)}");
                _masks.Add(b);
            }
        }

        return _masks[n];
    }

    protected override BucketBytes ConvertData(ref BucketBytes sourceData, bool final)
    {
        // We don't want to convert the final TagSize bytes using the normal convert
        int available = _byteCollector.Length + sourceData.Length;

        return DoConvertData(ref sourceData, available - TagSize, final);
    }

    protected override async ValueTask<(BucketBytes Result, BucketBytes SourceData)> ConvertDataAsync(BucketBytes sourceData, bool final)
    {
        int available = _byteCollector.Length + sourceData.Length;

        long? rem = final ? 0 : await Source.ReadRemainingBytesAsync().ConfigureAwait(false);

        // TODO: If 'rem' = 0, then we can pass final as true and complete in one step.

        if (!(rem > TagSize))
            available -= TagSize - (int)(rem ?? 0);

        var r = DoConvertData(ref sourceData, available, final);

        return (r, sourceData);
    }

    protected BucketBytes DoConvertData(ref BucketBytes sourceData, int available, bool final)
    {
        ReadOnlyMemory<byte> srcData;
        byte[] src = _buf16 ??= new byte[16];

        // For the normal reads, we never want to read the final 16 bytes "TAG"
        if (available < TagSize)
        {
            if (available < 0)
                return BucketBytes.Eof;

            _byteCollector.Append(sourceData);
            sourceData = final ? BucketBytes.Eof : BucketBytes.Empty;

            if (!final)
                return BucketBytes.Empty;

            srcData = _byteCollector.ToArray();
            _byteCollector.Clear();

            if (available > 0)
            {
                // We have remaining data
                Array.Clear(src, 0, src.Length);

                srcData.Slice(0, available).CopyTo(src);

                // Offset_* = Offset_m xor L_*
                SpanXor(_offset, GetMask(-2));
                // Pad = ENCIPHER(K, Offset_*)
                // P_ * = C_ * xor Pad[1..bitlen(C_ *)]
                SpanXor(src, Encipher(_offset));

                // Checksum_* = Checksum_m xor (P_* || 1 || zeros(127-bitlen(P_*)))
                src[available] = 0x80;
                if (available < BlockLength)
                    Array.Clear(src, available + 1, src.Length - available - 1);

                //Debug.WriteLine($"P*: {DumpData(src.AsSpan(0, available))}");

                // Tag = ENCIPHER(K, Checksum_ * xor Offset_ * xor L_$) xor HASH(K, A)
                SpanXor(_checksum, src);

                SpanXor(_checksum, _offset);
                SpanXor(_checksum, GetMask(-1));

            }
            else
            {
                // Tag = ENCIPHER(K, Checksum_m xor Offset_m xor L_$) xor HASH(K,A)
                SpanXor(_checksum, _offset);
                SpanXor(_checksum, GetMask(-1));
            }


            byte[] tag = Encipher(_checksum);
            SpanXor(tag, Hash(_associatedData).Span);

            bool ok = tag.AsSpan(0, TagSize).SequenceEqual(srcData.Slice(available, TagSize).Span);
            if (_verified is { })
                _verified(ok);
            else if (!ok)
                throw new BucketDecryptionException($"Decrypted data in {this} bucket not valid");


            return available > 0 ? new(src, 0, available) : BucketBytes.Eof;
        }

        available -= available % BlockLength;


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

        int convertBlocks = available / BlockLength;

        for (int i = 0; i < convertBlocks; i++)
        {
            ++_inChunkBlock;

            //Debug.WriteLine($"trailing zeros of {_inChunkBlock} is {TrailingZeros(_inChunkBlock)}");
            //Debug.WriteLine($"L_{TrailingZeros(_inChunkBlock)} is {DumpData(GetMask(TrailingZeros(_inChunkBlock)))}");

            SpanXor(_offset, GetMask(TrailingZeros(_inChunkBlock)));

            //Debug.WriteLine($"Offset_{_inChunkBlock} = {DumpData(_offset)}");

            Array.Clear(_buf16, 0, 16);
            srcData.Slice(i * BlockLength, BlockLength).CopyTo(_buf16);
            Span<byte> dest = _buffer2.AsSpan(i * BlockLength, BlockLength);

            SpanXor(src, _offset);
            Decrypt(src, dest);
            SpanXor(dest, _offset);

            SpanXor(_checksum, dest);
        }


        //Debug.WriteLine($"P: {DumpData(_buffer2.AsSpan(0, available))}");

        return new BucketBytes(_buffer2, 0, available);
    }

    private ReadOnlyMemory<byte> Hash(ReadOnlyMemory<byte> associatedData)
    {
        byte[] sum = new byte[16];
        byte[] offset = new byte[16];
        byte[] tmp = new byte[16];
        int blockNr = 1;

        while (blockNr * BlockLength <= associatedData.Length)
        {
            SpanXor(offset, GetMask(TrailingZeros(blockNr)));

            associatedData.Slice((blockNr - 1) * BlockLength, BlockLength).Span.CopyTo(tmp);
            SpanXor(tmp, offset);

            SpanXor(sum, Encipher(tmp));
            blockNr++;
        }

        int remaining = associatedData.Length % BlockLength;

        if (remaining > 0)
        {
            SpanXor(offset, GetMask(-2));

            Array.Clear(tmp, 0, 16);
            associatedData.Slice(associatedData.Length - remaining).CopyTo(tmp);
            tmp[remaining] = 0x80;

            SpanXor(tmp, offset);

            SpanXor(sum, Encipher(tmp));
        }
        // else sum = sim

        return sum;
    }

    protected override int ConvertRequested(int requested)
    {
        // TODO: Try to read enough to ensure a block, so keep track of Tag
        if (requested < BlockLength)
            return BlockLength;
        else
            return requested;
    }
}
