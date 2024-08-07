﻿using System.Buffers;
using System.Diagnostics;
#if !NET
using System.Runtime.InteropServices;
#endif
using System.Security.Cryptography;

namespace AmpScm.Buckets.Cryptography;

internal sealed class CfbMapper : SymmetricAlgorithm
{
    public CfbMapper(SymmetricAlgorithm algorithm)
    {
        Algorithm = algorithm;
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
        Algorithm.Mode = CipherMode.ECB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                Algorithm.Dispose();
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[]? rgbIV)
    {
        return new Decryptor(this, FeedbackSize, rgbIV);
    }

    public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[]? rgbIV)
    {
        throw new NotSupportedException("CFB encryptor not implemented yet");
    }

    public override void GenerateIV()
    {
        Algorithm.GenerateIV();
    }

    public override void GenerateKey()
    {
        Algorithm.GenerateKey();
    }

    public SymmetricAlgorithm Algorithm { get; }

    public override byte[] Key
    {
        get => Algorithm.Key;
        set => Algorithm.Key = value;
    }

    public override byte[] IV
    {
        get => Algorithm.IV;
        set => Algorithm.IV = value;
    }

    public override int KeySize
    {
        get => Algorithm.KeySize;
        set => Algorithm.KeySize = value;
    }

    private int _feedbackSize;
    public override int FeedbackSize
    {
        get => _feedbackSize;
        set => _feedbackSize = value;
    }

    public override int BlockSize
    {
        get => Algorithm.BlockSize;
        set => Algorithm.BlockSize = value;
    }

    public override KeySizes[] LegalBlockSizes => Algorithm.LegalBlockSizes;

    public override KeySizes[] LegalKeySizes => Algorithm.LegalKeySizes;

    private sealed class Decryptor : ICryptoTransform
    {
        private readonly byte[] _feedback;

        public Decryptor(CfbMapper mapper, int feedbackSize, byte[]? rgbIV)
        {
            Mapper = mapper;
            Algorithm = mapper.Algorithm;
            _feedback = rgbIV?.ToArray() ?? new byte[Algorithm.FeedbackSize];
        }

        public bool CanReuseTransform => false;

        public bool CanTransformMultipleBlocks => true;

        public int InputBlockSize => _feedback.Length;

        public int OutputBlockSize => _feedback.Length;

        public CfbMapper Mapper { get; }
        public SymmetricAlgorithm Algorithm { get; }

        public void Dispose()
        {
            //Transform.Dispose();
        }

        private byte[] Encipher(ReadOnlyMemory<byte> input)
        {
#if NET
            return Algorithm.EncryptEcb(input.Span, PaddingMode.None);
#else
            if (!MemoryMarshal.TryGetArray(input, out var seg))
                throw new InvalidOperationException();

            using var ec = Algorithm.CreateEncryptor();
            return ec.TransformFinalBlock(seg.Array!, seg.Offset, seg.Count);
#endif
        }

        internal static void SpanXor(Span<byte> a, ReadOnlySpan<byte> b)
            => OcbDecodeBucket.SpanXor(a, b);

        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            int blockSize = InputBlockSize;

            if (inputCount % blockSize != 0)
                throw new InvalidOperationException();

            int nWritten = 0;

            while (inputCount >= blockSize)
            {
                var r = Encipher(_feedback);

                var src = inputBuffer.AsSpan(inputOffset, blockSize);
                var target = outputBuffer.AsSpan(outputOffset, blockSize);

                r.CopyTo(target);
                SpanXor(target, src);
                src.CopyTo(_feedback);

                inputCount -= blockSize;
                inputOffset += blockSize;
                outputOffset += blockSize;
                nWritten += blockSize;
            }

            return nWritten;
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            int blockSize = InputBlockSize;

            if (inputCount % blockSize != 0)
                throw new InvalidOperationException();

            byte[] outputBuffer = new byte[inputCount];

            int n = TransformBlock(inputBuffer, inputOffset, inputCount, outputBuffer, 0);

            Debug.Assert(n == inputCount);

            return outputBuffer;
        }
    }

}
