﻿using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Cryptography
{
    internal sealed class RawDecryptBucket : ConversionBucket
    {
        private readonly SymmetricAlgorithm _algorithm;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly ICryptoTransform _transform;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private byte[]? _buffer;
        private ByteCollector _byteCollector;
        private readonly int _blocksizeBytes;

        public RawDecryptBucket(Bucket inner, SymmetricAlgorithm algorithm, bool decrypt)
            : base(inner)
        {
            _algorithm = algorithm;

            if (decrypt)
                _transform = _algorithm.CreateDecryptor();
            else
                _transform = _algorithm.CreateEncryptor();

            _blocksizeBytes = algorithm.BlockSize / 8;
        }

        protected override BucketBytes ConvertData(ref BucketBytes sourceData, bool final)
        {
            _byteCollector.Append(sourceData);
            sourceData = BucketBytes.Empty;

            if (final)
            {
                _buffer = null;
                return _transform.TransformFinalBlock(_byteCollector.ToArray(), 0, _byteCollector.Length);
            }
            else
            {
                byte[] toConvert = _byteCollector.ToArray();
                int convertSize = _byteCollector.Length - _byteCollector.Length % _blocksizeBytes;

                _buffer ??= new byte[1024];

                _byteCollector.Clear();
                if (convertSize < toConvert.Length)
                {
                    _byteCollector.Append(toConvert.AsMemory(convertSize).ToArray());
                }

                int n;
                if (convertSize > 0)
                    n = _transform.TransformBlock(toConvert, 0, convertSize, _buffer!, 0);
                else
                    n = 0;

                Debug.Assert(n == convertSize);

                return new BucketBytes(_buffer!, 0, n);
            }
        }

        protected override int ConvertRequested(int requested)
        {
            int needForRead = _blocksizeBytes - _byteCollector.Length;

            if (requested < needForRead)
                return needForRead;
            else
                return requested;
        }

        protected override void InnerDispose()
        {
            base.InnerDispose();

            _transform.Dispose();
            _algorithm.Dispose();
        }
    }
}
