using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Client;
using AmpScm.Buckets.Specialized;

// https://www.rfc-editor.org/rfc/rfc4880
// https://datatracker.ietf.org/doc/draft-koch-openpgp-2015-rfc4880bis/

namespace AmpScm.Buckets.Signatures
{
    public class PgpDecryptBucket : WrappingBucket
    {
        private readonly Func<ReadOnlyMemory<byte>, SignatureBucketKey?> _getKey;
        bool _inBody;
        OpenPgpContainer _container;
        Bucket? _reader;
        private SignatureBucketKey? _decryptKey; // Key towards decrypted
        private OpenPgpSymmetricAlgorithm _sessionAlgorithm;
        private ReadOnlyMemory<byte> _sessionKey; // The symetric key
        private string _fileName;
        OpenPgpContainer _q;

        public PgpDecryptBucket(Bucket inner, Func<ReadOnlyMemory<byte>, SignatureBucketKey?> getKey)
            : base(inner)
        {
            _getKey = getKey;
            _container = new OpenPgpContainer(inner);
            _q = _container;
        }

        async ValueTask ReadHeader()
        {
            if (_inBody)
                return;

            while (true)
            {
                var (bucket, tag) = await _q.ReadPacketAsync().ConfigureAwait(false);

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
                                case OpenPgpCompressionType.Zlib:
                                    rd = new ZLibBucket(bucket, BucketCompressionAlgorithm.ZLib);
                                    break;
                                default:
                                    throw new NotImplementedException($"Compression algorithm {(OpenPgpCompressionType)b} not implemented");
                            }

                            _q = new OpenPgpContainer(rd.AtEof(() => { }));
                            continue;
                        }

                    case OpenPgpTagType.PublicKeySession:
                        {
                            byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                            // Read the public key, for who the file is encrypted
                            var bb = (await bucket.ReadExactlyAsync(8).ConfigureAwait(false));

                            var key = _getKey(bb);

                            if (!_sessionKey.IsEmpty || key?.MatchFingerprint(bb) is not { } matchedKey)
                                break; // Ignore rest of packet

                            var pca = (OpenPgpPublicKeyType)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);

                            var keyValues = matchedKey.Values;

                            switch (matchedKey.Algorithm)
                            {
                                case SignatureBucketAlgorithm.Rsa:
                                    using (var rsa = RSA.Create())
                                    {
                                        BigInteger D = MakeBigInt(keyValues[2]);
                                        BigInteger P = MakeBigInt(keyValues[3]);
                                        BigInteger Q = MakeBigInt(keyValues[4]);

                                        BigInteger DP = D % (P - 1);
                                        BigInteger DQ = D % (Q - 1);

                                        var p = new RSAParameters()
                                        {
                                            Modulus = MakeUnsignedArray(keyValues[0]),
                                            Exponent = MakeUnsignedArray(keyValues[1]),
                                            D = MakeUnsignedArray(keyValues[2]),
                                            P = MakeUnsignedArray(keyValues[3]),
                                            Q = MakeUnsignedArray(keyValues[4]),
                                            InverseQ = MakeUnsignedArray(keyValues[5]),
                                            DP = BIToArray(DP),
                                            DQ = BIToArray(DQ),
                                        };

                                        var bi = await SignatureBucket.ReadPgpMultiPrecisionInteger(bucket).ConfigureAwait(false);

                                        rsa.ImportParameters(p);

                                        var data = rsa.Decrypt(bi.Value.ToArray(), RSAEncryptionPadding.Pkcs1);
                                        ushort checksum = NetBitConverter.ToUInt16(data, data.Length - 2);

                                        if (checksum == data.Skip(1).Take(data.Length - 3).Sum(x => (ushort)x))
                                        {
                                            _decryptKey = key;
                                            _sessionAlgorithm = (OpenPgpSymmetricAlgorithm)data[0];
                                            _sessionKey = data.AsMemory(1, data.Length - 3); // Minus first byte (session alg) and last two bytes (checksum)
                                        }
                                    }
                                    break;

                                case SignatureBucketAlgorithm.Curve25519:


                                default:
                                    throw new NotImplementedException();
                            }
                        }
                        break;
                    case OpenPgpTagType.OnePassSignature:
                        {
                            byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            byte signatureType = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            var hashAlgorithm = (OpenPgpHashAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
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
                    case OpenPgpTagType.SymetricEncryptedIntegrity:
                        {
                            byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                            switch (_sessionAlgorithm)
                            {
                                case OpenPgpSymmetricAlgorithm.Aes:
                                case OpenPgpSymmetricAlgorithm.Aes256:
                                    Aes aes = Aes.Create();
                                    aes.Mode = CipherMode.CFB;
                                    aes.Key = _sessionKey.ToArray();
                                    aes.IV = new byte[aes.IV.Length];
                                    aes.Padding = PaddingMode.None;
                                    aes.FeedbackSize = aes.BlockSize;

                                    var dcb = new RawDecryptBucket(bucket, aes, true);

                                    var bb = await dcb.ReadExactlyAsync(aes.BlockSize / 8 + 2).ConfigureAwait(false);

                                    if (bb[bb.Length - 1] != bb[bb.Length - 3] || bb[bb.Length - 2] != bb[bb.Length - 4])
                                        throw new InvalidOperationException("AES-256 decrypt failed");

                                    _q = new OpenPgpContainer(dcb);
                                    continue;
                                default:
                                    throw new NotImplementedException();
                            }

                            return;
                        }
                    case OpenPgpTagType.UserID:
#if DEBUG
                        {
                            var bb = await bucket.ReadExactlyAsync(MaxRead).ConfigureAwait(false);

                            Trace.WriteLine(bb.ToUTF8String());
                        }
#endif
                        break;
                    case OpenPgpTagType.Literal:
                        {
                            byte dataType = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            byte fileNameLen = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                            if (fileNameLen > 0)
                            {
                                var fn = await bucket.ReadExactlyAsync(fileNameLen).ConfigureAwait(false);
                                _fileName = fn.ToASCIIString();
                            }
                            else
                                _fileName = "";

                            var dt = await bucket.ReadExactlyAsync(4).ConfigureAwait(false); // Date

                            _inBody = true;
                            _reader = bucket.AtEof(() =>
                                {
                                    _reader = null;
                                    _inBody = false;
                                });
                            return;
                        }
                        break;
                    case OpenPgpTagType.SymetricSessionKey:
                    case OpenPgpTagType.Signature:
                        break;
                    default:
                        break;
                }

                await bucket.ReadUntilEofAsync().ConfigureAwait(false);
            }

            static byte[] MakeUnsignedArray(ReadOnlyMemory<byte> readOnlyMemory)
                => SignatureBucket.MakeUnsignedArray(readOnlyMemory);

            static BigInteger MakeBigInt(ReadOnlyMemory<byte> readOnlyMemory)
            {
#if NETCOREAPP
                return new BigInteger(readOnlyMemory.ToArray(), true, true);
#else
                var r = MakeUnsignedArray(readOnlyMemory);

                Array.Reverse(r);

                return new BigInteger(r);
#endif
            }

            static byte[] BIToArray(BigInteger value)
            {
                var b = value.ToByteArray();
                Array.Reverse(b);

                return MakeUnsignedArray(b);
            }
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
        {
            await ReadHeader();


            var bb = await (_reader ?? _container).ReadAsync(requested);

            if (!_inBody && bb.IsEmpty)
                await ReadHeader();

            return bb;
        }

        public override BucketBytes Peek()
        {
            return base.Peek();
        }
    }
}
