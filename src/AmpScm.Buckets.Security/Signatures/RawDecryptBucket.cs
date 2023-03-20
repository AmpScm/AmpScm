using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Signatures
{
    sealed class RawDecryptBucket : ConversionBucket
    {
        readonly SymmetricAlgorithm _algorithm;
        readonly ICryptoTransform _transform;
        readonly bool _decrypt;
        byte[]? _buffer;
        ByteCollector _byteCollector;
        readonly int _blocksizeBytes;

        public RawDecryptBucket(Bucket inner, SymmetricAlgorithm algorithm, bool decrypt)
            : base(inner)
        {
            _algorithm = algorithm;
            _decrypt = decrypt;

            if (decrypt)
                _transform = _algorithm.CreateDecryptor();
            else
                _transform = _algorithm.CreateEncryptor();

            _blocksizeBytes = algorithm.BlockSize / 8;
        }

        protected override BucketBytes ConvertData(ref BucketBytes sourceData, bool final)
        {
            _byteCollector.Append(sourceData);

            if ((_buffer?.Length ?? 0) < _byteCollector.Length)
                _buffer = new byte[_byteCollector.Length];

            if (final)
            {
                _buffer = null;
                return _transform.TransformFinalBlock(_byteCollector.ToArray(), 0, _byteCollector.Length);
            }
            else
            {
                byte[] toConvert = _byteCollector.ToArray();
                int convertSize = _byteCollector.Length - _byteCollector.Length % _blocksizeBytes;

                _byteCollector.Clear();
                if (convertSize < toConvert.Length)
                {
                    _byteCollector.Append(toConvert.AsMemory(convertSize).ToArray());
                }

                int n = _transform.TransformBlock(toConvert, 0, convertSize, _buffer, 0);

                Debug.Assert(n == convertSize);

                return new BucketBytes(_buffer, 0, n);
            }
        }

        protected override int ConvertRequested(int requested)
        {
            return MaxRead;// Math.Min(Math.Min(requested - requested % _blocksizeBytes, _blocksizeBytes), 8192);
        }

        protected override async ValueTask<BucketBytes> InnerReadAsync(int requested = 2146435071)
        {
            // HACK: Hides an issue in the AES code,
            var b = Inner.Buffer(16 * 1024 * 1024);
            await b.ReadUntilEofAsync().ConfigureAwait(false);
            b.Reset();

            return await b.ReadExactlyAsync(requested);
        }

        protected override void InnerDispose()
        {
            base.InnerDispose();

            _transform.Dispose();
            _algorithm.Dispose();
        }
    }
}
