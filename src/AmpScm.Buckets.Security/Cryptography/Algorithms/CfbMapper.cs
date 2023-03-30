using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Cryptography
{
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
            throw new NotImplementedException();
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

        int _feedbackSize;
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
            readonly byte[] _feedback;

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
#if NETCOREAPP
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
                int blockSize = Algorithm.BlockSize / 8;

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
                int blockSize = Algorithm.BlockSize / 8;
                byte[] outputBuffer = new byte[inputCount + blockSize];

                int nWritten = 0;
                int outputOffset = 0;

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

                if (inputCount > 0)
                {
                    var r = Encipher(_feedback);

                    var src = inputBuffer.AsSpan(inputOffset);
                    var op = outputBuffer.AsSpan(outputOffset, blockSize);

                    r.AsSpan(0, src.Length).CopyTo(op);
                    SpanXor(op, _feedback);
                    src.CopyTo(_feedback);

                    nWritten += src.Length;
                }

                return outputBuffer.AsSpan(0, nWritten).ToArray();
            }
        }

    }
}
