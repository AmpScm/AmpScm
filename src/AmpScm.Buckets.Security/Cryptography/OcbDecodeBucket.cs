using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Cryptography;

public sealed class OcbDecodeBucket : ConversionBucket
{
    private static readonly byte[] _zero16 = new byte[16];
    private readonly Aes _aes;
    private readonly ReadOnlyMemory<byte> _associatedData;
    private byte[]? _buffer2;
    private int _inChunkBlock;
    private ByteCollector _byteCollector;
    private readonly byte[] _offset;
    private readonly byte[] _checksum;
    private readonly byte[] _buf16;
    private readonly Action<bool>? _verified;

    public const int MaxNonceLength = 15;
    public const int BlockLength = 16;

    /// <summary>
    /// Length of the tag in bytes
    /// </summary>
    private int TagSize { get; }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="source"></param>
    /// <param name="aesKey"></param>
    /// <param name="tagLen">Length of the tag in bits</param>
    /// <param name="nonce"></param>
    /// <param name="associatedData"></param>
    /// <param name="verifyResult"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public OcbDecodeBucket(Bucket source, byte[] aesKey, int tagLen, ReadOnlyMemory<byte> nonce, ReadOnlyMemory<byte> associatedData = default, Action<bool>? verifyResult = null)
        : base(source.Leave(tagLen / 8, new OcbLeaver() is { } leaver ? leaver.Left : throw new InvalidOperationException()))
    {
        if (tagLen < 8 || tagLen > BlockLength * 8 || tagLen % 8 != 0)
            throw new ArgumentOutOfRangeException(nameof(tagLen), tagLen, message: null);
        else if (nonce.Length > MaxNonceLength)
            throw new ArgumentOutOfRangeException(nameof(nonce), nonce, message: null);

        leaver.Bucket = this;

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

        byte[] stretched = kTop.Concat(ArrayXor(kTop.AsMemory(0, 8), kTop.AsMemory(1, 8))).ToArray();

        _offset = GetBits(stretched, bottom, 128);

        _checksum = new byte[16];
        TagSize = tagLen / 8;
        _verified = verifyResult;
        _associatedData = associatedData;

        _buf16 = new byte[16];
    }



    protected override void Dispose(bool disposing)
    {
        try
        {
            _aes.Dispose();
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

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

        data[last] = (byte)(from[last] << 1 ^ ((from[0] >> 7) * 0b10000111));
    }

    private static byte[] GetBits(byte[] array, int pos, int bits)
    {
        if (pos + bits > array.Length * 8)
            throw new ArgumentOutOfRangeException(nameof(bits), bits, message: null);

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
#if NET
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
#if NET
        return _aes.EncryptEcb(input.Span, PaddingMode.None);
#else
        if (!MemoryMarshal.TryGetArray(input, out var seg))
            throw new InvalidOperationException();

        using var ec = _aes.CreateEncryptor();
        return ec.TransformFinalBlock(seg.Array!, seg.Offset, seg.Count);
#endif
    }

    private byte[] Decrypt(ReadOnlyMemory<byte> input)
    {
#if NET
        return _aes.DecryptEcb(input.Span, PaddingMode.None);
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

                _masks.Add(b);
            }
        }

        return _masks[n];
    }

    private byte[]? _annotation;
    private bool _verifyLater;

    private void LeftData(BucketBytes bb)
    {
        if (!_verifyLater)
        {
            // We received the data before we were done reading
            _annotation = bb.ToArray();
            if (_annotation.Length != TagSize)
                throw new BucketException($"Couldn't fetch final tag of {this} bucket");
        }
        else
        {
            // We are already done reading. The calculated tag is cached for us
            byte[] tag = _annotation!;

            var annotation = bb.ToArray();
            if (annotation.Length != TagSize)
                throw new BucketException($"Couldn't fetch final tag of {this} bucket");

            _verifyLater = false; // State now same as normal scenario

            bool ok = tag.AsSpan(0, TagSize).SequenceEqual(annotation);
            if (_verified is { })
                _verified(ok);
            else if (!ok)
                throw new BucketDecryptionException($"Decrypted data in {this} bucket not valid");

            _annotation = null;
        }
    }

    private void DoVerify()
    {
        byte[] tag = Encipher(_checksum);
        SpanXor(tag, Hash(_associatedData).Span);

        if (_annotation is null)
        {
            _verifyLater = true;
            _annotation = tag;
        }
        else
        {
            bool ok = tag.AsSpan(0, TagSize).SequenceEqual(_annotation);
            if (_verified is { })
                _verified(ok);
            else if (!ok)
                throw new BucketDecryptionException($"Decrypted data in {this} bucket not valid");

            _annotation = null;
        }
    }

    protected override BucketBytes ConvertData(ref BucketBytes sourceData, bool final)
    {
        ReadOnlyMemory<byte> srcData;
        //byte[] src = _buf16;
        _buffer2 ??= new byte[8192];
        int offset = 0;

        if (!_byteCollector.IsEmpty || final)
        {
            int nFromSrc = Math.Min(BlockLength - _byteCollector.Length, sourceData.Length);
            _byteCollector.Append(sourceData.Slice(0, nFromSrc));
            if (nFromSrc > 0)
                sourceData = sourceData.Slice(nFromSrc);

            if (_byteCollector.Length < BlockLength)
            {
                if (!final)
                    return sourceData = BucketBytes.Empty; // Wait for more

                // Handle the final (few) bytes

                srcData = _byteCollector.ToArray();
                _byteCollector.Clear();

                if (srcData.Length > 0)
                {
                    TransformBlock(srcData.Span, ref offset);

                    SpanXor(_checksum, _offset);
                    SpanXor(_checksum, GetMask(-1));
                }
                else
                {
                    // Tag = ENCIPHER(K, Checksum_m xor Offset_m xor L_$) xor HASH(K,A)
                    SpanXor(_checksum, _offset);
                    SpanXor(_checksum, GetMask(-1));
                }

                DoVerify();
                sourceData = BucketBytes.Eof;

                return srcData.Length > 0 ? new(_buffer2, 0, srcData.Length) : BucketBytes.Eof;
            }

            TransformBlock(_byteCollector.ToArray(), ref offset);
            _byteCollector.Clear();
        }

        while (sourceData.Length >= BlockLength && offset <= _buffer2.Length - BlockLength)
        {
            TransformBlock(sourceData.Span.Slice(0, BlockLength), ref offset);
            sourceData = sourceData.Slice(BlockLength);
        }

        if (sourceData.Length < BlockLength && !sourceData.IsEmpty)
        {
            _byteCollector.Append(sourceData);
            sourceData = BucketBytes.Empty;
        }

        return new(_buffer2, 0, offset);
    }

    private void TransformBlock(ReadOnlySpan<byte> srcData, ref int offset)
    {
        byte[] src = _buf16;

        if (srcData.Length == BlockLength)
        {
            ++_inChunkBlock;

            SpanXor(_offset, GetMask(TrailingZeros(_inChunkBlock)));

            Array.Clear(_buf16, 0, 16);
            srcData.CopyTo(_buf16);
            Span<byte> dest = _buffer2.AsSpan(offset, BlockLength);

            SpanXor(src, _offset);
            Decrypt(src, dest);
            SpanXor(dest, _offset);

            SpanXor(_checksum, dest);

            offset += 16;
        }
        else
        {
            // We have remaining data
            Array.Clear(src, 0, src.Length);

            srcData.CopyTo(src);

            // Offset_* = Offset_m xor L_*
            SpanXor(_offset, GetMask(-2));
            // Pad = ENCIPHER(K, Offset_*)
            // P_ * = C_ * xor Pad[1..bitlen(C_ *)]
            SpanXor(src, Encipher(_offset));

            // Checksum_* = Checksum_m xor (P_* || 1 || zeros(127-bitlen(P_*)))
            src[srcData.Length] = 0x80;
            if (srcData.Length < BlockLength)
                Array.Clear(src, srcData.Length + 1, src.Length - srcData.Length - 1);

            src.CopyTo(_buffer2.AsSpan(offset, BlockLength));

            // Tag = ENCIPHER(K, Checksum_ * xor Offset_ * xor L_$) xor HASH(K, A)
            SpanXor(_checksum, src);
        }
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

    private sealed class OcbLeaver
    {
        public void Left(BucketBytes bb, long length)
        {
            Bucket?.LeftData(bb);
        }

        public OcbDecodeBucket? Bucket { get; set; }
    }
}
