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

namespace AmpScm.Buckets.Signatures
{
    public class PgpDecryptBucket : WrappingBucket
    {
        private readonly Func<ReadOnlyMemory<byte>, SignatureBucketKey?> _getKey;
        bool _inBody;
        OpenPgpContainer _container;
        Bucket? _reader;
        private SignatureBucketKey? _decryptKey;
        private byte[]? _sessionKey;

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

                            // Read the public key, for who the file is encrypted
                            var bb = (await bucket.ReadExactlyAsync(8).ConfigureAwait(false));

                            var key = _getKey(bb);
                            
                            if (key?.MatchFingerprint(bb) is not { } matchedKey)
                                break; // Ignore rest of packet

                            var pca = (OpenPgpPublicKeyType)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);

                            switch(matchedKey.Algorithm)
                            {
                                case SignatureBucketAlgorithm.Rsa:
                                    using (var rsa = RSA.Create())
                                    {
                                        BigInteger D = MakeBigInt(key.Values[2]);
                                        BigInteger P = MakeBigInt(key.Values[3]);
                                        BigInteger Q = MakeBigInt(key.Values[4]);

                                        BigInteger DP = D % (P - 1);
                                        BigInteger DQ = D % (Q - 1);
                                        BigInteger InverseQ = ModInverse(P, Q);

                                        var p = new RSAParameters()
                                        {
                                            Modulus = MakeUnsignedArray(key.Values[0]),
                                            Exponent = MakeUnsignedArray(key.Values[1]),
                                            D = MakeUnsignedArray(key.Values[2]),
                                            P = MakeUnsignedArray(key.Values[3]),
                                            Q = MakeUnsignedArray(key.Values[4]),
                                            DP = BIToArray(DP),
                                            DQ = BIToArray(DQ),
                                            InverseQ = BIToArray(InverseQ),
                                        };

                                        var bi = await SignatureBucket.ReadPgpMultiPrecisionInteger(bucket).ConfigureAwait(false);

                                        rsa.ImportParameters(p);

                                        var r = MakeUnsignedArray(bi.Value);

                                        _sessionKey = rsa.Decrypt(r, RSAEncryptionPadding.Pkcs1);
                                    }
                                    break;

                                default:
                                    throw new NotImplementedException();
                            }
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
                    case OpenPgpTagType.SymetricEncryptedIntegrity:
                        {
                            byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            _reader = bucket.AtEof(() => { _reader = null; _inBody = false; });
                            _inBody = true;
                            return;
                        }
                    default:
                        break;
                }

                await bucket.ReadUntilEofAsync().ConfigureAwait(false);
            }

            static BigInteger ModInverse(BigInteger a, BigInteger n)
            {
                BigInteger t = 0, nt = 1, r = n, nr = a;

                if (n < 0)
                {
                    n = -n;
                }

                if (a < 0)
                {
                    a = n - (-a % n);
                }

                while (nr != 0)
                {
                    var quot = r / nr;

                    var tmp = nt; nt = t - quot * nt; t = tmp;
                    tmp = nr; nr = r - quot * nr; r = tmp;
                }

                if (r > 1) throw new ArgumentException(nameof(a) + " is not convertible.");
                if (t < 0) t = t + n;
                return t;
            }

            static byte[] MakeUnsignedArray(ReadOnlyMemory<byte> readOnlyMemory)
            {
                int n = readOnlyMemory.Length & 3;

                if (n == 1 && readOnlyMemory.Span[0] == 0 && (readOnlyMemory.Length & 1) == 1)
                    return readOnlyMemory.Slice(1).ToArray();
                else if (n == 3)
                {
                    var  nw = new byte[readOnlyMemory.Length+1];

                    readOnlyMemory.CopyTo(new Memory<byte>(nw, 1, readOnlyMemory.Length));
                    return nw;
                }
                else
                    return readOnlyMemory.ToArray();
            }

            static BigInteger MakeBigInt(ReadOnlyMemory<byte> readOnlyMemory) 
            {
#if NETCOREAPP
                return new BigInteger(readOnlyMemory.ToArray(), false, true);
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
            

            return await (_reader ?? _container).ReadAsync(requested);
        }

        public override BucketBytes Peek()
        {
            return base.Peek();
        }
    }
}
