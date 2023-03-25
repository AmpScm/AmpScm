using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Client;
using AmpScm.Buckets.Cryptography;
using AmpScm.Buckets.Specialized;

// https://www.rfc-editor.org/rfc/rfc4880
// https://datatracker.ietf.org/doc/draft-koch-openpgp-2015-rfc4880bis/

namespace AmpScm.Buckets.Cryptography
{
    public sealed class DecryptBucket : CryptoDataBucket
    {
#pragma warning disable CA2213 // Disposable fields should be disposed
        private bool _inBody;
        private readonly OpenPgpContainer _container;
        private Bucket? _reader;
        private Signature? _decryptKey; // Key towards decrypted
        private OpenPgpSymmetricAlgorithm _sessionAlgorithm;
        private ReadOnlyMemory<byte> _sessionKey; // The symetric key
        private string? _fileName;
        private DateTime? _fileDate;
        private OpenPgpContainer _q;
        private readonly Stack<PgpSignature> _sigs = new();
#pragma warning restore CA2213 // Disposable fields should be disposed

        private new OpenPgpContainer Source => (OpenPgpContainer)base.Source;

        public DecryptBucket(Bucket source)
            : base(new OpenPgpContainer(source))
        {
            _container = Source;
            _q = _container;
        }


        public Func<ReadOnlyMemory<byte>, Signature?>? GetKey { get; init; }
        public Func<SignaturePromptContext, string>? GetPassword { get; init; }

        private async ValueTask ReadHeader()
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
                            {
                                await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                                break; // Skip packet, we already have a session
                            }

                            var key = GetKey?.Invoke(bb);

                            if (key?.MatchFingerprint(bb) is not { } matchedKey)
                            {
                                await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                                break; // Ignore rest of packet
                            }

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

                                        byte[] data = rsa.Decrypt(bi.Value.ToArray(), RSAEncryptionPadding.Pkcs1);
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

                            byte[] signer = (await bucket.ReadExactlyAsync(8).ConfigureAwait(false)).ToArray();

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
                            byte[] startingVector = (await bucket.ReadExactlyAsync(OcbDecodeBucket.MaxNonceLength).ConfigureAwait(false)).ToArray();

                            // Encrypted data

                            bucket = bucket.Leave(16, (_, _) => { });

                            bucket = new AeadChunkReader(bucket, (int)chunk_size, 16,
                                (n, data) =>
                                {
                                    byte[] associatedData = new byte[] { 0xC0 | (int)OpenPgpTagType.OCBEncryptedData, version, (byte)cipherAlgorithm, aeadAlgorithm, chunkVal }
                                        .Concat(NetBitConverter.GetBytes((long)n)).ToArray();


                                    byte[] sv = startingVector.ToArray();

                                    OcbDecodeBucket.SpanXor(sv.AsSpan(sv.Length - 4), NetBitConverter.GetBytes(n));

                                    return new OcbDecodeBucket(data, _sessionKey.ToArray(), 128, sv, associatedData, verifyResult: x =>
                                    {
                                        if (!x)
                                            throw new BucketDecryptionException($"Verification of chunk {n + 1} in {data} bucket failed");
                                    });
                                });

                            _q = new OpenPgpContainer(bucket);

                            continue;
                        }
                    case OpenPgpTagType.SymetricEncryptedIntegrity:
                        {
                            byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            var b = await StartDecrypt(bucket).ConfigureAwait(false);

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

                    case OpenPgpTagType.SymetricSessionKey:
                        {
                            if (!_sessionKey.IsEmpty)
                                break; // Ignore session key if we already have one

                            byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                            var cipherAlgorithm = (OpenPgpSymmetricAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
                            if (version == 5)
                            {
                                byte ocb = (await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);

                                Debug.Assert(ocb == 2);
                            }

                            var s2k = await SignatureBucket.ReadPgpS2kSpecifierAsync(bucket).ConfigureAwait(false);

                            byte[]? key = null;
                            if (GetPassword?.Invoke(SignaturePromptContext.Empty) is { } password)
                            {
                                key = SignatureBucket.DeriveS2kKey(cipherAlgorithm, s2k, password);
                            }

                            if (version == 5 && key is { })
                            {
                                byte[] nonce = (await bucket.ReadExactlyAsync(OcbDecodeBucket.MaxNonceLength).ConfigureAwait(false)).ToArray(); // always 15, as OCB length

                                bool? verified_as_ok = null;

                                using var ocb = new OcbDecodeBucket(bucket, key.ToArray(), 128, nonce,
                                    associatedData: new byte[] { (byte)(0xc0 | (int)tag), version, (byte)cipherAlgorithm, 0x02 /* s2k type */ },
                                    verifyResult: (result) => verified_as_ok = result);

                                var k = await ocb.ReadExactlyAsync(17).ConfigureAwait(false);

                                Debug.Assert(verified_as_ok != null, "Verify not called");
#pragma warning disable CA1508 // Avoid dead conditional code // Bad diagnostic, as used in lambda.
                                if (k.Length == 16 && verified_as_ok == true)
#pragma warning restore CA1508 // Avoid dead conditional code
                                {
                                    _sessionAlgorithm = cipherAlgorithm;
                                    _sessionKey = k.ToArray();
                                }
                            }

                        }
                        break;
                    case OpenPgpTagType.Signature:
                        {
                            var r = await SignatureBucket.ParseSignatureAsync(bucket).ConfigureAwait(false);

                            var p = _sigs.Pop();

                            byte[]? hashValue = p.Completer(r.signBlob);
                            Trace.WriteLine(BucketExtensions.HashToString(hashValue));

                            if (NetBitConverter.ToUInt16(hashValue, 0) != r.hashStart)
                                throw new BucketException("Hash failed");
                        }
                        break;
                    default:
                        await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                        break;
                }

                long n = await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                Debug.Assert(n == 0, $"Unread data left in {bucket} of tagType {tag}");
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
                byte[] b = value.ToByteArray();
                Array.Reverse(b);

                return MakeUnsignedArray(b);
            }
        }

        private async ValueTask<Bucket> StartDecrypt(Bucket bucket)
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

                    var dcb = new RawDecryptBucket(bucket, aes, true);

                    var bb = await dcb.ReadExactlyAsync(aes.BlockSize / 8 + 2).ConfigureAwait(false);

                    if (bb[bb.Length - 1] != bb[bb.Length - 3] || bb[bb.Length - 2] != bb[bb.Length - 4])
                        throw new InvalidOperationException("AES-256 decrypt failed");

                    return dcb;

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
            await ReadHeader().ConfigureAwait(false);


            var bb = await (_reader ?? _container).ReadAsync(requested);

            if (!_inBody && bb.IsEmpty)
                await ReadHeader().ConfigureAwait(false);

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

        private record PgpSignature
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
