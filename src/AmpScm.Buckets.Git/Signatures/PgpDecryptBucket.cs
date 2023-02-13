using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Client;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Signatures
{
    public class PgpDecryptBucket : WrappingBucket
    {
        private readonly Func<ReadOnlyMemory<byte>, SignatureBucketKey?> _getKey;
        bool _inBody;

        OpenPgpContainer _container;

        public PgpDecryptBucket(Bucket inner, Func<ReadOnlyMemory<byte>, SignatureBucketKey?> getKey)
            : base(inner)
        {
            _getKey = getKey;
            _container = new OpenPgpContainer(inner);
        }

        async ValueTask ReadHeader()
        {
            if (_inBody)
                return;

            var q = _container;
            while (true)
            {
                var (bucket, tag) = await q.ReadPacketAsync().ConfigureAwait(false);

                if (bucket is null)
                    return;

                switch (tag)
                {
                    case OpenPgpTagType.CompressedData:
                        {
                            byte? b = await bucket.ReadByteAsync().ConfigureAwait(false);

                            Bucket rd;
                            switch ((OpenPgpCompressionType)b)
                            {
                                case OpenPgpCompressionType.None:
                                    rd = bucket;
                                    break;
                                case OpenPgpCompressionType.Zip:
                                    rd = new ZLibBucket(bucket, BucketCompressionAlgorithm.Deflate);
                                    break;
                                default:
                                    throw new NotImplementedException($"Compression algorithm {(OpenPgpCompressionType)b} not implemented");
                            }

                            q = new OpenPgpContainer(rd);
                            continue;
                        }

                    case OpenPgpTagType.PublicKeySession:
                        {
                            byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                            var bb = (await bucket.ReadExactlyAsync(8).ConfigureAwait(false)).ToArray();

                            var pca = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                            OpenPgpPublicKeyType pkt = (OpenPgpPublicKeyType)pca;

                            var encryptedSessionkey = await bucket.ReadExactlyAsync(Bucket.MaxRead).ConfigureAwait(false);
                        }
                        break;
                    case OpenPgpTagType.OnePassSignature:
                        {
                            byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            byte signatureType = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            byte hashAlgorithm = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            var pca = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                            OpenPgpPublicKeyType pkt = (OpenPgpPublicKeyType)pca;
                            var signer = (await bucket.ReadExactlyAsync(8).ConfigureAwait(false)).ToArray();

                            byte flag = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                            GC.KeepAlive(flag);
                        }
                        break;
                    case OpenPgpTagType.AEADEncryptedData:
                        {
                            byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            byte cipherAlgorithm = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            byte aeadAlgorithm = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            byte chunkVal = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                            long chunk_size = 1L << (chunkVal + 6);

                            // Starting vector (aead specific)

                            // Encrypted data

                            var pca = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                            OpenPgpPublicKeyType pkt = (OpenPgpPublicKeyType)pca;
                            var signer = (await bucket.ReadExactlyAsync(8).ConfigureAwait(false)).ToArray();

                            byte flag = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                        }
                        break;
                }

                await bucket.ReadUntilEofAsync().ConfigureAwait(false);
            }
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
        {
            await ReadHeader();
            

            return await _container.ReadAsync(requested);
        }
    }
}
