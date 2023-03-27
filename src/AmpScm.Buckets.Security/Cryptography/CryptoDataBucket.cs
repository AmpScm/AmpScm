using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Cryptography
{

    public abstract class CryptoDataBucket : WrappingBucket
    {
        private protected CryptoDataBucket(Bucket source) : base(source)
        {
        }

        private protected static int GetKeySize(OpenPgpSymmetricAlgorithm cipherAlgorithm)
        {
            return cipherAlgorithm switch
            {
                OpenPgpSymmetricAlgorithm.Aes => 128,
                OpenPgpSymmetricAlgorithm.Aes192 => 192,
                OpenPgpSymmetricAlgorithm.Aes256 => 256,
                _ => throw new NotImplementedException()
            };
        }

        private protected record struct S2KSpecifier(OpenPgpHashAlgorithm HashAlgorithm, byte[]? Salt, int HashByteCount, OpenPgpSymmetricAlgorithm CipherAlgorithm);

        private protected static byte[] DeriveS2kKey(S2KSpecifier s2k, string password)
        {
            int bitsRequired = GetKeySize(s2k.CipherAlgorithm);

            int bytesRequired = bitsRequired / 8;
            List<byte> result = new();
            int zeros = 0;

            while (result.Count < bytesRequired)
            {
                IEnumerable<byte> pwd = Encoding.UTF8.GetBytes(password);

                if (s2k.Salt != null)
                {
                    pwd = s2k.Salt.Concat(pwd);
                }

                if (zeros > 0)
                    pwd = Enumerable.Range(0, zeros).Select(_ => (byte)0).Concat(pwd);
                zeros++;

                byte[] toHash = pwd.ToArray();

                using HashAlgorithm ha = CreatePgpHashAlgorithm(s2k.HashAlgorithm);

                if (s2k.HashByteCount <= toHash.Length)
                {
                    result.AddRange(ha.ComputeHash(toHash));
                    continue;
                }

                int nHashBytes = s2k.HashByteCount;
                do
                {
                    int n = Math.Min(nHashBytes, toHash.Length);
                    ha.TransformBlock(toHash, 0, n, null, 0);

                    nHashBytes -= n;
                }
                while (nHashBytes > 0);

                ha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                result.AddRange(ha.Hash!);
            }

            return result.Take(bytesRequired).ToArray();
        }

        internal static string FingerprintToString(ReadOnlyMemory<byte> fingerprint)
        {
            if (fingerprint.Length == 0)
                throw new ArgumentNullException(nameof(fingerprint));

            byte b0 = fingerprint.Span[0];

            if (b0 >= 3 && b0 <= 5) // OpenPgp fingeprint formats 3-5
                return string.Join("", Enumerable.Range(1, fingerprint.Length - 1).Select(i => fingerprint.Span[i].ToString("X2", CultureInfo.InvariantCulture)));
            else if (b0 == 0 && fingerprint.Span[1] == 0 && fingerprint.Span[2] == 0)
            {
                var vals = ParseSshStrings(fingerprint);

#if NETCOREAPP
                string b64 = Convert.ToBase64String(fingerprint.Span);
#else
                string b64 = Convert.ToBase64String(fingerprint.ToArray());
#endif

                return $"{Encoding.ASCII.GetString(vals[0].ToArray())} {b64}";
            }

            return "";
        }

        internal static ReadOnlyMemory<byte>[] GetEcdsaValues(IEnumerable<ReadOnlyMemory<byte>> vals, bool pgp = false)
        {
            var curve = vals.First();
            byte[] v2 = vals.Skip(1).First().ToArray();

            if (pgp)
            {
                string curveName;

                if (curve.Span.SequenceEqual(new byte[] { 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07 }))
                    curveName = nameof(ECCurve.NamedCurves.nistP256);
                else if (curve.Span.SequenceEqual(new byte[] { 0x2B, 0x81, 0x04, 0x00, 0x22 }))
                    curveName = nameof(ECCurve.NamedCurves.nistP384);
                else if (curve.Span.SequenceEqual(new byte[] { 0x2B, 0x81, 0x04, 0x00, 0x23 }))
                    curveName = nameof(ECCurve.NamedCurves.nistP521);
                else if (curve.Span.SequenceEqual(new byte[] { 0x2B, 0x24, 0x03, 0x03, 0x02, 0x08, 0x01, 0x01, 0x07 }))
                    curveName = nameof(ECCurve.NamedCurves.brainpoolP256r1);
                else if (curve.Span.SequenceEqual(new byte[] { 0x2B, 0x24, 0x03, 0x03, 0x02, 0x08, 0x01, 0x01, 0x0D }))
                    curveName = nameof(ECCurve.NamedCurves.brainpoolP512t1);
                else
                    throw new NotImplementedException("Unknown curve oid in ecdsa key");

#pragma warning disable CA1308 // Normalize strings to uppercase
                curve = Encoding.ASCII.GetBytes(curveName.ToLowerInvariant());
#pragma warning restore CA1308 // Normalize strings to uppercase
            }

            return v2[0] switch
            {
                4 => // X and Y follow
                     // X and Y both have the same number of bits... Half the value
                    new[]
                    {
                // The Curve name is stored as integer... Nice :(.. But at least consistent
                curve,

                v2.Skip(1).Take(v2.Length / 2).ToArray(),
                v2.Skip(1 + v2.Length / 2).Take(v2.Length / 2).ToArray(),
                    },

                0x40 when pgp // Custom compressed poing see rfc4880bis-06
                    => new[]
                    {
                curve,

                v2.Skip(1).ToArray(),
                    },
                2       // Y is even
                or 3    // Y is odd
                or _ =>
                    // TODO: Find some implementation to calculate X from Y
                    throw new NotImplementedException("Only X and Y follow format is supported at this time"),
            };

        }

        private protected static byte[] MakeUnsignedArray(ReadOnlyMemory<byte> readOnlyMemory)
        {
            int n = readOnlyMemory.Length & 3;

            if (n == 1 && readOnlyMemory.Span[0] == 0 && (readOnlyMemory.Length & 1) == 1)
                return readOnlyMemory.Slice(1).ToArray();
            else if (n == 3)
            {
                byte[] nw = new byte[readOnlyMemory.Length + 1];

                readOnlyMemory.CopyTo(new Memory<byte>(nw, 1, readOnlyMemory.Length));
                return nw;
            }
            else
                return readOnlyMemory.ToArray();
        }

        internal static ReadOnlyMemory<byte>[] ParseSshStrings(ReadOnlyMemory<byte> data)
        {
            List<ReadOnlyMemory<byte>> mems = new();

            // HACK^2: We know we have a memory only bucket, so ignore everything async
            // And we also know the result will refer to the original data, so returning
            // references is safe in this specific edge case.

            var b = data.AsBucket();

            while (ReadSshStringAsync(b).AsTask().Result is BucketBytes bb && !bb.IsEof)
            {
                mems.Add(bb.Memory);
            }

            return mems.ToArray();
        }



        private protected static async ValueTask<S2KSpecifier> ReadPgpS2kSpecifierAsync(Bucket bucket, OpenPgpSymmetricAlgorithm algorithm)
        {
            byte type = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
            OpenPgpHashAlgorithm alg;
            byte[] salt;

            switch (type)
            {
                case 0:
                    { // Simple S2K
                        alg = (OpenPgpHashAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);

                        return new(alg, null, 0, algorithm);
                    }
                case 1:
                    { // Salted S2k
                        alg = (OpenPgpHashAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
                        salt = (await bucket.ReadExactlyAsync(8).ConfigureAwait(false)).ToArray();

                        return new(alg, salt, 0, algorithm);
                    }
                // 2 : reserved
                case 3:
                    { // Iterated and Salted S2K
                        alg = (OpenPgpHashAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
                        salt = (await bucket.ReadExactlyAsync(8).ConfigureAwait(false)).ToArray();
                        int count = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                        int c = 16 + (count & 0xF) << ((count >> 4) + 6);

                        return new(alg, salt, c, algorithm);
                    }
                default:
                    throw new NotImplementedException();
            }
        }



        private protected static async ValueTask<byte[]> CalculateHash(Bucket sourceData, OpenPgpHashAlgorithm hashAlgorithm)
        {
            byte[]? result = null;
            using var sd = sourceData.Hash(CreatePgpHashAlgorithm(hashAlgorithm), x => result = x);

            await sd.ReadUntilEofAsync().ConfigureAwait(false);

#pragma warning disable CA1508 // Avoid dead conditional code
            return result ?? throw new InvalidOperationException();
#pragma warning restore CA1508 // Avoid dead conditional code
        }

        private protected static HashAlgorithm CreatePgpHashAlgorithm(OpenPgpHashAlgorithm hashAlgorithm)
        {
            return hashAlgorithm switch
            {
                OpenPgpHashAlgorithm.SHA256 => SHA256.Create(),
                OpenPgpHashAlgorithm.SHA384 => SHA384.Create(),
                OpenPgpHashAlgorithm.SHA512 => SHA512.Create(),
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
                OpenPgpHashAlgorithm.SHA1 => SHA1.Create(),
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
                OpenPgpHashAlgorithm.MD5 => MD5.Create(),
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms

#if NETFRAMEWORK
                OpenPgpHashAlgorithm.MD160 => RIPEMD160.Create(),
#endif

                _ => throw new NotImplementedException($"Hash algorithm {hashAlgorithm} is not supported.")
            };
        }

        private protected static Bucket CreateHasher(Bucket? bucket, OpenPgpHashAlgorithm hashAlgorithm, Action<Func<byte[]?, byte[]>> completer)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (completer is null)
                throw new ArgumentNullException(nameof(completer));

            return bucket.Hash(CreatePgpHashAlgorithm(hashAlgorithm), completer);
        }

        private protected static HashAlgorithmName GetDotNetHashAlgorithmName(OpenPgpHashAlgorithm hashAlgorithm)
        => hashAlgorithm switch
        {
            OpenPgpHashAlgorithm.SHA256 => HashAlgorithmName.SHA256,
            OpenPgpHashAlgorithm.SHA512 => HashAlgorithmName.SHA512,
            OpenPgpHashAlgorithm.SHA384 => HashAlgorithmName.SHA384,
            OpenPgpHashAlgorithm.SHA1 => HashAlgorithmName.SHA1,
            OpenPgpHashAlgorithm.MD5 => HashAlgorithmName.MD5,
            _ => throw new NotImplementedException($"OpenPGP scheme {hashAlgorithm} not mapped yet.")
        };

        private protected static SignatureAlgorithm GetKeyAlgo(OpenPgpPublicKeyType keyPublicKeyType)
        => keyPublicKeyType switch
        {
            OpenPgpPublicKeyType.Rsa => SignatureAlgorithm.Rsa,
            OpenPgpPublicKeyType.Dsa => SignatureAlgorithm.Dsa,
            OpenPgpPublicKeyType.ECDSA => SignatureAlgorithm.Ecdsa,
            OpenPgpPublicKeyType.Ed25519 => SignatureAlgorithm.Ed25519,
            OpenPgpPublicKeyType.ECDH => SignatureAlgorithm.Ecdh,
            OpenPgpPublicKeyType.Curve25519 => SignatureAlgorithm.Curve25519,
            OpenPgpPublicKeyType.Elgamal => SignatureAlgorithm.Elgamal,
            _ => throw new ArgumentOutOfRangeException(nameof(keyPublicKeyType), keyPublicKeyType, null)
        };

        private protected static async ValueTask<uint?> ReadLengthAsync(Bucket bucket)
        {
            return (await OpenPgpContainer.ReadLengthAsync(bucket).ConfigureAwait(false)).Length;
        }

        private protected static async ValueTask<BucketBytes> ReadSshStringAsync(Bucket bucket)
        {
            var bb = await bucket.ReadExactlyAsync(sizeof(int)).ConfigureAwait(false);

            if (bb.IsEof)
                return BucketBytes.Eof;
            else if (bb.Length < sizeof(int))
                throw new BucketEofException(bucket);

            int len = NetBitConverter.ToInt32(bb, 0);

            if (len == 0)
                return BucketBytes.Empty;

            return await bucket.ReadExactlyAsync(len).ConfigureAwait(false);
        }

        private protected static async ValueTask<ReadOnlyMemory<byte>?> ReadPgpMultiPrecisionInteger(Bucket sourceData)
        {
            var bb = await sourceData.ReadExactlyAsync(2).ConfigureAwait(false);
            if (bb.IsEof)
                return null;
            else if (bb.Length != 2)
                throw new BucketEofException(sourceData);

            ushort bitLen = NetBitConverter.ToUInt16(bb, 0);

            if (bitLen == 0)
                return null;
            else
            {
                int byteLen = (bitLen + 7) / 8;
                bb = await sourceData.ReadExactlyAsync(byteLen).ConfigureAwait(false);

                if (bb.Length != byteLen)
                    throw new BucketEofException(sourceData);

                return bb.ToArray();
            }
        }

        private protected static bool SplitSignatureInt(int index, OpenPgpPublicKeyType signaturePublicKeyType)
        {
            return signaturePublicKeyType == OpenPgpPublicKeyType.ECDSA && index == 0;
        }

        private protected static async ValueTask SequenceToList(List<ReadOnlyMemory<byte>> vals, DerBucket der2)
        {
            while (true)
            {
                var (b, bt) = await der2.ReadValueAsync().ConfigureAwait(false);

                if (b is null)
                    break;

                if (bt == DerType.Sequence)
                {
                    using var bq = new DerBucket(b);
                    await SequenceToList(vals, bq).ConfigureAwait(false);
                }
                else
                {
                    var bb = await b!.ReadExactlyAsync(32768).ConfigureAwait(false);

                    if (bt == DerType.BitString)
                    {
                        // This next check matches for DSA.
                        // I'm guessing this is some magic
                        if (bb.Span.StartsWith(new byte[] { 0, 0x02, 0x81, 0x81 }) || bb.Span.StartsWith(new byte[] { 0, 0x02, 0x81, 0x80 }))
                            vals.Add(bb.Slice(4).ToArray());
                        else
                            vals.Add(bb.ToArray());
                    }
                    else
                        vals.Add(bb.ToArray());

                    await b.ReadUntilEofAndCloseAsync().ConfigureAwait(false);
                }
            }
        }


        private protected static Bucket CreateDecryptBucket(Bucket source, OpenPgpSymmetricAlgorithm algorithm, byte[] key, byte[]? iv = null)
        {
            switch (algorithm)
            {
                case OpenPgpSymmetricAlgorithm.Aes:
                case OpenPgpSymmetricAlgorithm.Aes192:
                case OpenPgpSymmetricAlgorithm.Aes256:
                    var aes = Aes.Create();
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
                    aes.Mode = CipherMode.CFB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
                    aes.KeySize = GetKeySize(algorithm);
                    aes.Key = key;
                    aes.IV = iv ?? new byte[aes.BlockSize / 8];
                    aes.Padding = PaddingMode.None;
                    aes.FeedbackSize = aes.BlockSize;

                    return new RawDecryptBucket(source, aes, true);

                default:
                    throw new NotImplementedException($"Not implemented for {algorithm} algorithm yet");
            }
        }

        private protected static async ValueTask<Bucket> StartDecryptLoadIV(Bucket bucket, OpenPgpSymmetricAlgorithm algorithm, byte[] sessionKey)
        {
            switch (algorithm)
            {
                case OpenPgpSymmetricAlgorithm.Aes:
                case OpenPgpSymmetricAlgorithm.Aes192:
                case OpenPgpSymmetricAlgorithm.Aes256:
                    var dcb = CreateDecryptBucket(bucket, algorithm, sessionKey, null);
                    var bb = await dcb.ReadExactlyAsync(GetKeySize(algorithm) / 8 + 2).ConfigureAwait(false);

                    if (bb[bb.Length - 1] != bb[bb.Length - 3] || bb[bb.Length - 2] != bb[bb.Length - 4])
                        throw new InvalidOperationException("AES-256 decrypt failed");

                    return dcb;

                default:
                    throw new NotImplementedException();
            }
        }

        private protected record struct SignatureInfo(OpenPgpSignatureType SignatureType, byte[]? Signer, OpenPgpPublicKeyType PublicKeyType, OpenPgpHashAlgorithm HashAlgorithm, ushort HashStart, DateTimeOffset? SignTime, byte[]? SignBlob, ReadOnlyMemory<byte>[] SignatureInts, byte[]? SignKeyFingerprint);

        private protected static async ValueTask<SignatureInfo> ParseSignatureAsync(Bucket bucket)
        {
            OpenPgpSignatureType signatureType;
            byte[]? signer = null;
            OpenPgpPublicKeyType publicKeyType;
            OpenPgpHashAlgorithm hashAlgorithm;
            ushort hashStart;
            DateTime? signTime = null;
            byte[]? signBlob;
            ReadOnlyMemory<byte>[]? signatureInts;
            byte[]? signKeyFingerprint = null;

            byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(bucket);

            if (version == 4 || version == 5)
            {
                /* - One-octet version number (4).
                 * - One-octet signature type.
                 * - One-octet public-key algorithm.
                 * - One-octet hash algorithm.
                 * - Two-octet scalar octet count for following hashed subpacket data.
                 *   Note that this is the length in octets of all of the hashed
                 *   subpackets; a pointer incremented by this number will skip over
                 *   the hashed subpackets.
                 * - Hashed subpacket data set (zero or more subpackets).
                 * - Two-octet scalar octet count for the following unhashed subpacket
                 *   data.  Note that this is the length in octets of all of the
                 *   unhashed subpackets; a pointer incremented by this number will
                 *   skip over the unhashed subpackets.
                 * - Unhashed subpacket data set (zero or more subpackets).
                 * - Two-octet field holding the left 16 bits of the signed hash value.
                 * - One or more multiprecision integers comprising the signature.
                 *   This portion is algorithm specific, as described above.
                 */

                int hdrLen = version == 4 ? 5 : 7;

                var bb = await bucket.ReadExactlyAsync(hdrLen).ConfigureAwait(false);

                if (bb.Length != hdrLen)
                    throw new BucketEofException(bucket);

                ByteCollector bc = new ByteCollector(1024);
                bc.Append(version);
                bc.Append(bb);

                signatureType = (OpenPgpSignatureType)bb[0];
                publicKeyType = (OpenPgpPublicKeyType)bb[1];
                hashAlgorithm = (OpenPgpHashAlgorithm)bb[2];
                int subLen;

                if (version == 4)
                    subLen = NetBitConverter.ToUInt16(bb, 3);
                else
                    subLen = (int)NetBitConverter.ToUInt32(bb, 3);

                if (subLen > 0)
                {
                    using var subRead = bucket.NoDispose().TakeExactly(subLen)
                        .AtRead(bb =>
                        {
                            if (!bb.IsEmpty)
                                bc.Append(bb);

                            return new();
                        });

                    while (true)
                    {
                        uint? len = await ReadLengthAsync(subRead).ConfigureAwait(false);

                        if (!len.HasValue)
                            break;

                        byte b = await subRead.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(subRead);
                        len--;

                        switch ((OpenPgpSubPacketType)b)
                        {
                            case OpenPgpSubPacketType.SignatureCreationTime:
                                if (len != 4)
                                    throw new InvalidOperationException();

                                int time = await subRead.ReadNetworkInt32Async().ConfigureAwait(false);

                                signTime = DateTimeOffset.FromUnixTimeSeconds(time).DateTime;
                                break;
                            case OpenPgpSubPacketType.Issuer:
                                signer = (await subRead.ReadExactlyAsync((int)len).ConfigureAwait(false)).ToArray();
                                break;
                            case OpenPgpSubPacketType.IssuerFingerprint:
                                signKeyFingerprint = (await subRead.ReadExactlyAsync((int)len).ConfigureAwait(false)).ToArray();
                                break;

                            // Currently unhandled fields from private keys
                            case OpenPgpSubPacketType.KeyFlags:
                            case OpenPgpSubPacketType.KeyExpirationTime:
                            case OpenPgpSubPacketType.PreferredSymetricAlgorithms:
                            case OpenPgpSubPacketType.PreferredHashAlgorithms:
                            case OpenPgpSubPacketType.PreferredCompressionAlgorithms:
                            case OpenPgpSubPacketType.Features:
                            case OpenPgpSubPacketType.KeyServerPreferences:
                            case OpenPgpSubPacketType.PreferredAeadAlgorithms:
                                if (len != await subRead.ReadSkipAsync(len.Value).ConfigureAwait(false))
                                    throw new BucketEofException(subRead);
                                break;

                            case OpenPgpSubPacketType.SignersUserID:
                                if (len != await subRead.ReadSkipAsync(len.Value).ConfigureAwait(false))
                                    throw new BucketEofException(subRead);
                                break;
                            default:
                                if (len != await subRead.ReadSkipAsync(len.Value).ConfigureAwait(false))
                                    throw new BucketEofException(subRead);
                                break;
                        }
                    }
                }

                // TODO: Fetch Date and Issuer if needed
                uint unhashedLen;

                if (version == 4)
                    unhashedLen = await bucket.ReadNetworkUInt16Async().ConfigureAwait(false);
                else
                    unhashedLen = await bucket.ReadNetworkUInt32Async().ConfigureAwait(false);

                if (unhashedLen > 0)
                {
                    if (unhashedLen != await bucket.ReadSkipAsync(unhashedLen).ConfigureAwait(false))
                        throw new BucketEofException(bucket);
                }

                // First 2 bytes of hash
                hashStart = await bucket.ReadNetworkUInt16Async().ConfigureAwait(false);

                if (version != 4)
                {
                    // In v5, 16 bytes of salt
                    bb = await bucket.ReadExactlyAsync(16).ConfigureAwait(false);
                    if (bb.Length != 16)
                        throw new BucketEofException(bucket);
                }

                // Complete the signblob
                byte[] lenBytes;

                if (version == 4)
                    lenBytes = NetBitConverter.GetBytes(bc.Length);
                else // v5
                    lenBytes = NetBitConverter.GetBytes((long)bc.Length);

                bc.Append(version);
                bc.Append(0xFF);
                bc.Append(lenBytes);

                signBlob = bc.ToArray();
            }
            else if (version == 3)
            {
                // Implementations MUST accept V3 signatures.
                /*
                 *      - One-octet version number (3).
                 *      - One-octet length of following hashed material.  MUST be 5.
                 *          - One-octet signature type.
                 *          - Four-octet creation time.
                 *      - Eight-octet key ID of signer.
                 *      - One-octet public key algorithm.
                 *      - One-octet hash algorithm.
                 *      - Two-octet field holding left 16 bits of signed hash value.
                 */
                var bb = await bucket.ReadExactlyAsync(18).ConfigureAwait(false);
                if (bb[0] != 5)
                    throw new BucketException($"HashInfoLen must by 5 for v3 in {bucket.Name}");
                signatureType = (OpenPgpSignatureType)bb[1];
                signTime = DateTimeOffset.FromUnixTimeSeconds(NetBitConverter.ToUInt32(bb, 2)).DateTime;
                signer = bb.Slice(6, 8).ToArray();
                publicKeyType = (OpenPgpPublicKeyType)bb[14];
                hashAlgorithm = (OpenPgpHashAlgorithm)bb[15];
                hashStart = NetBitConverter.ToUInt16(bb, 16);

                signBlob = bb.Slice(1, 5).ToArray();
            }
            else
                throw new NotImplementedException("Only OpenPGP signature versions 3, 4 and 5 are supported");

            List<ReadOnlyMemory<byte>> bigInts = new();
            while (await ReadPgpMultiPrecisionInteger(bucket).ConfigureAwait(false) is ReadOnlyMemory<byte> bi)
            {
                bigInts.Add(bi);
            }

            signatureInts = bigInts.ToArray();

            if (publicKeyType == OpenPgpPublicKeyType.EdDSA && signatureInts.Length == 2 && signatureInts.All(x => x.Length == 32))
            {
                publicKeyType = OpenPgpPublicKeyType.Ed25519;
                signatureInts = new ReadOnlyMemory<byte>[] { signatureInts.SelectMany(x => x.ToArray()).ToArray() };
            }
            else if (publicKeyType == OpenPgpPublicKeyType.Dsa)
            {
                signatureInts = new ReadOnlyMemory<byte>[] { signatureInts.SelectMany(x => x.ToArray()).ToArray() };
            }

            return new(signatureType, signer, publicKeyType, hashAlgorithm, hashStart, signTime, signBlob, signatureInts, signKeyFingerprint);
        }

        private protected static bool VerifySignature(SignatureInfo signatureInfo, byte[] hashValue, IReadOnlyList<ReadOnlyMemory<byte>> keyValues)
        {
            if (signatureInfo.SignatureInts == null)
                throw new InvalidOperationException("No signature value found to verify against");

            switch (signatureInfo.PublicKeyType)
            {
                case OpenPgpPublicKeyType.Rsa:

                    using (var rsa = RSA.Create())
                    {
                        byte[] signature = signatureInfo.SignatureInts![0].ToArray();

                        rsa.ImportParameters(new RSAParameters()
                        {
                            Modulus = MakeUnsignedArray(keyValues[0]),
                            Exponent = MakeUnsignedArray(keyValues[1])
                        });

                        return rsa.VerifyHash(hashValue, signature, GetDotNetHashAlgorithmName(signatureInfo.HashAlgorithm), RSASignaturePadding.Pkcs1);
                    }
                case OpenPgpPublicKeyType.Dsa:
                    using (var dsa = DSA.Create())
                    {
                        byte[] signature = signatureInfo.SignatureInts![0].ToArray();

                        try
                        {
                            dsa.ImportParameters(new DSAParameters()
                            {
                                P = MakeUnsignedArray(keyValues[0]),
                                Q = MakeUnsignedArray(keyValues[1]),
                                G = MakeUnsignedArray(keyValues[2]),
                                Y = MakeUnsignedArray(keyValues[3])
                            });
                        }
                        catch (ArgumentException ex)
                        {
                            throw new InvalidOperationException("DSA parameter length error. Please verify edge case in public key", ex);
                        }

                        return dsa.VerifySignature(hashValue, signature);
                    }
                case OpenPgpPublicKeyType.Elgamal:
                    // P, G, Y
                    goto default;
                case OpenPgpPublicKeyType.ECDSA:
                    using (var ecdsa = ECDsa.Create())
                    {
                        string curveName = Encoding.ASCII.GetString(keyValues[0].ToArray());

                        // Signature must be concattenation of 2 values with same number of bytes
                        byte[] r = MakeUnsignedArray(signatureInfo.SignatureInts[0]);
                        byte[] s = MakeUnsignedArray(signatureInfo.SignatureInts[1]);

                        int klen = Math.Max(r.Length, s.Length);

                        if (curveName.EndsWith("p256", StringComparison.Ordinal))
                            klen = 32;
                        else if (curveName.EndsWith("p384", StringComparison.Ordinal))
                            klen = 48;
                        else if (curveName.EndsWith("p521", StringComparison.Ordinal))
                            klen = 66;

                        byte[] sig = new byte[2 * klen];

                        r.CopyTo(sig, klen - r.Length);
                        s.CopyTo(sig, 2 * klen - s.Length);

                        ecdsa.ImportParameters(new ECParameters()
                        {
                            // The name is stored as integer... Nice :(
                            Curve = ECCurve.CreateFromFriendlyName(curveName),

                            Q = new ECPoint
                            {
                                X = keyValues[1].ToArray(),
                                Y = keyValues[2].ToArray(),
                            },
                        });

                        return ecdsa.VerifyHash(hashValue, sig);
                    }
                case OpenPgpPublicKeyType.Ed25519:
                    {
                        byte[] signature = signatureInfo.SignatureInts![0].ToArray();

                        return Chaos.NaCl.Ed25519.Verify(signature, hashValue, keyValues[0].ToArray());
                    }
                case OpenPgpPublicKeyType.EdDSA:
                default:
                    throw new NotImplementedException($"Public Key type {signatureInfo.PublicKeyType} not implemented yet");
            }
        }
    }
}
