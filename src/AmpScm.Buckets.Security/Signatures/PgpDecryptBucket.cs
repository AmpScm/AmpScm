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
#pragma warning disable CA2213 // Disposable fields should be disposed
        bool _inBody;
        OpenPgpContainer _container;
        Bucket? _reader;
        private Signature? _decryptKey; // Key towards decrypted
        private OpenPgpSymmetricAlgorithm _sessionAlgorithm;
        private ReadOnlyMemory<byte> _sessionKey; // The symetric key
        private string? _fileName;
        private DateTime? _fileDate;
        private OpenPgpContainer _q;
        private Stack<PgpSignature> _sigs = new();
#pragma warning restore CA2213 // Disposable fields should be disposed

        public PgpDecryptBucket(Bucket inner, Func<ReadOnlyMemory<byte>, Signature?>? getKey)
            : base(inner)
        {
            GetKey = getKey;
            _container = new OpenPgpContainer(inner);
            _q = _container;
        }


        public Func<ReadOnlyMemory<byte>, Signature?>? GetKey { get; init; }
        public Func<string>? GetPassword { get; init; }

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

                            _q = new OpenPgpContainer(rd);
                            continue;
                        }

                    case OpenPgpTagType.PublicKeySession:
                        {
                            byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                            // Read the public key, for who the file is encrypted
                            var bb = (await bucket.ReadExactlyAsync(8).ConfigureAwait(false));

                            if (!_sessionKey.IsEmpty)
                                break; // Skip packet, we already have a session

                            var key = GetKey?.Invoke(bb);

                            if (key?.MatchFingerprint(bb) is not { } matchedKey)
                                break; // Ignore rest of packet

                            var pca = (OpenPgpPublicKeyType)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);

                            var keyValues = matchedKey.Values;

                            switch (matchedKey.Algorithm)
                            {
                                case SignatureAlgorithm.Rsa:
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

                                case SignatureAlgorithm.Curve25519:


                                default:
                                    throw new NotImplementedException();
                            }
                        }
                        break;
                    case OpenPgpTagType.OnePassSignature:
                        {
                            byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            var signatureType = (OpenPgpSignatureType)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
                            var hashAlgorithm = (OpenPgpHashAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
                            var pkt = (OpenPgpPublicKeyType)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);

                            var signer = (await bucket.ReadExactlyAsync(8).ConfigureAwait(false)).ToArray();

                            byte flag = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                            GC.KeepAlive(flag);

                            _sigs.Push(new(signatureType, hashAlgorithm, pkt, signer));
                        }
                        break;
                    case OpenPgpTagType.OCBEncryptedData:
                        {
                            byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            var cipherAlgorithm = (OpenPgpSymmetricAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
                            byte aeadAlgorithm = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            byte chunkVal = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                            long chunk_size = 1L << (chunkVal + 6); // Usually 4MB

                            if (_sessionKey.IsEmpty)
                                throw new BucketException("Can't decrypt without valid session key");
                            else if (cipherAlgorithm != _sessionAlgorithm)
                                throw new BucketException("Session key is for different algorithm");

                            // Starting vector (OCB specific)
                            var startingVector = (await bucket.ReadExactlyAsync(15).ConfigureAwait(false)).ToArray();

                            // Encrypted data

                            Bucket b = await StartDecrypt(bucket, startingVector, chunk_size).ConfigureAwait(false);

                            _q = new OpenPgpContainer(b);

                            //Aes a;
                            //a.

                            continue;

                            var pca = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                            OpenPgpPublicKeyType pkt = (OpenPgpPublicKeyType)pca;
                            var signer = (await bucket.ReadExactlyAsync(8).ConfigureAwait(false)).ToArray();

                            byte flag = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                        }
                        break;
                    case OpenPgpTagType.SymetricEncryptedIntegrity:
                        {
                            byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            var b = await StartDecrypt(bucket, null, 0).ConfigureAwait(false);

                            _q = new OpenPgpContainer(b);
                            continue;
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
                            _fileDate = DateTimeOffset.FromUnixTimeSeconds(NetBitConverter.ToUInt32(dt, 0)).DateTime;
                            _inBody = true;

                            foreach (var s in _sigs)
                            {
                                switch (s.HashAlgorithm)
                                {
                                    case OpenPgpHashAlgorithm.SHA512:
                                        {
                                            var s2 = s;
                                            bucket = bucket.Hash(SHA512.Create(), x => s.Completer = x);
                                        }
                                        break;
                                }
                            }

                            _reader = bucket.AtEof(() =>
                                {
                                    _reader = null;
                                    _inBody = false;
                                });
                            return;
                        }
                        break;
                    case OpenPgpTagType.SymetricSessionKey:
                        {
                            byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            var cipherAlgorithm = (OpenPgpSymmetricAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
                            if (version == 5)
                            {
                                var ocb = (await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);

                                Debug.Assert(ocb == 2);
                            }

                            var s2k = await SignatureBucket.ReadPgpS2kSpecifierAsync(bucket).ConfigureAwait(false);

                            byte[]? key = null;
                            if (GetPassword?.Invoke() is { } password)
                            {
                                key = SignatureBucket.DeriveS2kKey(cipherAlgorithm, s2k, password);
                            }

                            if (version == 5 && key is { })
                            {
                                byte[] iv = (await bucket.ReadExactlyAsync(15).ConfigureAwait(false)).ToArray(); // always 15, as OCB length

                                var bb = await bucket.ReadExactlyAsync(16+16).ConfigureAwait(false);

                                var ocb = new OCBDecoder(bb.Memory.AsBucket(), OCBDecoder.SetupAes(key), 8192, 16, iv);

                                var k = await ocb.ReadExactlyAsync(16).ConfigureAwait(false);


                                //byte[] encryptedKey = (await bucket.ReadExactlyAsync(16).ConfigureAwait(false)).ToArray(); // Length of cipher-alg key
                                //byte[] authTag = (await bucket.ReadExactlyAsync(16).ConfigureAwait(false)).ToArray(); // For OCB
                            }

                        }
                        break;
                    case OpenPgpTagType.Signature:
                        {
                            var r = await SignatureBucket.ParseSignatureAsync(bucket).ConfigureAwait(false);

                            var p = _sigs.Pop();

                            var hashValue = p.Completer(r.signBlob);
                            Trace.WriteLine(BucketExtensions.HashToString(hashValue));

                            if (NetBitConverter.ToUInt16(hashValue, 0) != r.hashStart)
                                throw new BucketException("Hash failed");
                        }
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

        private async ValueTask<Bucket> StartDecrypt(Bucket bucket, byte[]? iv, long chunkSize)
        {
            switch (_sessionAlgorithm)
            {
                case OpenPgpSymmetricAlgorithm.Aes:
                case OpenPgpSymmetricAlgorithm.Aes192:
                case OpenPgpSymmetricAlgorithm.Aes256:
                    var aes = Aes.Create();
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
                    aes.Mode = CipherMode.CFB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
                    aes.Key = _sessionKey.ToArray();
                    aes.IV = new byte[aes.BlockSize / 8];
                    aes.Padding = PaddingMode.None;
                    aes.FeedbackSize = aes.BlockSize;

                    if (iv is { })
                    {
                        return new OCBDecoder(bucket, aes, chunkSize, 16, iv);
                    }
                    else
                    {
                        var dcb = new RawDecryptBucket(bucket, aes, true);

                        var bb = await dcb.ReadExactlyAsync(aes.BlockSize / 8 + 2).ConfigureAwait(false);

                        if (bb[bb.Length - 1] != bb[bb.Length - 3] || bb[bb.Length - 2] != bb[bb.Length - 4])
                            throw new InvalidOperationException("AES-256 decrypt failed");

                        return dcb;
                    }


                default:
                    throw new NotImplementedException();
            }
        }

        public async ValueTask<(string? fileName, DateTime? fileTime)> ReadFileInfo()
        {
            await ReadHeader().ConfigureAwait(false);

            return (_fileName, _fileDate);
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
        {
            await ReadHeader();


            var bb = await (_reader ?? _container).ReadAsync(requested);

            if (!_inBody && bb.IsEmpty)
                await ReadHeader();

            return bb;

            //while (true)
            //{
            //    if (_reader != null)
            //    {
            //        var bb = await _reader.ReadAsync().ConfigureAwait(false);
            //
            //        if (!bb.IsEof)
            //            return bb;
            //    }
            //
            //    await ReadHeader().ConfigureAwait(false);
            //
            //    if (_reader == null)
            //    {
            //        var bb = await _container.ReadAsync().ConfigureAwait(false);
            //
            //        return bb;
            //    }
            //
            //}
        }

        public override BucketBytes Peek()
        {
            return base.Peek();
        }

        record PgpSignature
        {
            public PgpSignature(OpenPgpSignatureType signatureType, OpenPgpHashAlgorithm hashAlgorithm, OpenPgpPublicKeyType pkt, byte[] signer)
            {
                SignatureType = signatureType;
                HashAlgorithm = hashAlgorithm;
                Pkt = pkt;
                Signer = signer;
            }

            public OpenPgpSignatureType SignatureType { get; }
            public OpenPgpHashAlgorithm HashAlgorithm { get; }
            public OpenPgpPublicKeyType Pkt { get; }
            public byte[] Signer { get; }
            public Func<byte[]?, byte[]?> Completer { get; internal set; }
        }
    }
}
