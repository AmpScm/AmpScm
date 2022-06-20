using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Git
{
    public enum OpenPgpTagType
    {
        /// <summary>Reserved</summary>
        None = 0,
        PublicKeySession = 1,
        Signature = 2,
        SymetricSessionKey = 3,
        OnePassSignature = 4,
        SecretKey = 5,
        PublicKey = 6,
        SecretSubkey = 7,
        CompressedData = 8,
        SymetricEncryptedData = 9,
        Marker = 10,
        Literal = 11,
        Trust = 12,
        UserID = 13,
        PublicSubkey = 14,
        // 15-59 undefined yet
        //60 to 63 -- Private or Experimental Values
    }

    enum OpenPgpSignatureType : byte
    {
        BinaryDocument = 0x00,
        CanonicalTextDocument = 0x01, // EOL -> CRLF
    }

    enum OpenPgpSubPacketType : byte
    {
        /// <summary>Reserved</summary>
        None = 0,
        SignatureCreationTime = 2,
        SignatureExpirationTime = 3,
        Issuer = 16,
        IssuerFingerprint = 33,
    }

    enum OpenPgpHashAlgorithm : byte
    {
        None,
        MD5 = 1,
        SHA1 = 2,
        MD160 = 3,
        SHA256 = 8,
        SHA384 = 9,
        SHA512 = 10,
        SHA224 = 11
    }

    enum OpenPgpPublicKeyType
    {
        None,
        Rsa = 1,
        RsaEncryptOnly = 2,
        RsaSignOnly = 3,
        Elgamal = 16,
        Dsa = 17,
        EllipticCurve = 18,
        ECDSA = 19,
        DHE = 21,
        EdDSA = 22,
        AEDH = 23,
        AEDSA = 24,

        // Outside PGP range, used for ssh
        Ed25519 = 0x1001,
    }

    /// <summary>
    /// Reads an OpenPGP or OpenSSH signature
    /// </summary>
    public class GitSignatureBucket : WrappingBucket
    {
        private OpenPgpSignatureType _signatureType;
        private byte[]? _signer;
        private OpenPgpPublicKeyType _signaturePublicKeyType;
        private OpenPgpHashAlgorithm _hashAlgorithm;
        private ushort _hashStart;
        DateTime? _signTime;
        private byte[]? _signature;
        private byte[]? _signBlob;
        private byte[]? _fingerPrint;
        private BigInteger[]? _signatureInts;
        private DateTime _keyTime;
        private OpenPgpPublicKeyType _keyPublicKeyType;
        private BigInteger[]? _keyInts;
        private byte[]? _keyFingerprint;


        new OpenPgpContainer Inner => (OpenPgpContainer)base.Inner;

        public GitSignatureBucket(Bucket inner)
            : base(new OpenPgpContainer(inner))
        {
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
        {
            await ReadAsync().ConfigureAwait(false);

            var bb = await Inner.ReadAsync(requested).ConfigureAwait(false);

            if (!bb.IsEof)
                throw new BucketException($"Unexpected trailing data in {Inner.Name} Bucket");

            return bb;
        }


        public async ValueTask ReadAsync()
        {
            var q = Inner;
            while (true)
            {
                var (bucket, tag) = await q.ReadPacketAsync().ConfigureAwait(false);

                if (bucket is null || tag is null)
                    return;

                using (bucket)
                {
                    switch (tag.Value)
                    {
                        case OpenPgpTagType.Signature when Inner.IsSshSignature:
                            {
                                uint sshVersion = await bucket.ReadNetworkUInt32Async().ConfigureAwait(false);

                                if (sshVersion != 1)
                                    throw new BucketException();

                                var bb = await ReadSshStringAsync(bucket).ConfigureAwait(false);
                                _signer = bb.ToArray();

                                using (var ib = _signer.AsBucket())
                                {
                                    bb = await ReadSshStringAsync(ib).ConfigureAwait(false);
                                    string alg = bb.ToASCIIString();

                                    if (alg.StartsWith("sk-", StringComparison.Ordinal))
                                        alg = alg.Substring(3);

                                    switch (alg)
                                    {
                                        case "ssh-rsa":
                                            _signaturePublicKeyType = OpenPgpPublicKeyType.Rsa;
                                            break;
                                        case "ssh-dss":
                                            _signaturePublicKeyType = OpenPgpPublicKeyType.Dsa;
                                            break;
                                        case "ssh-ed25519":
                                        case "ssh-ed25519@openssh.com":
                                            _signaturePublicKeyType = OpenPgpPublicKeyType.Ed25519;
                                            break;
                                        case "ecdsa-sha2-nistp256":
                                        case "ecdsa-sha2-nistp384":
                                        case "ecdsa-sha2-nistp521":
                                            _signaturePublicKeyType = OpenPgpPublicKeyType.ECDSA;
                                            break;
                                        default:
                                            throw new NotImplementedException($"Unknown signature type: {alg}");
                                    }

                                    List<BigInteger> keyInts = new();
                                    while (0 < await ib.ReadRemainingBytesAsync().ConfigureAwait(false))
                                    {
                                        bb = await ReadSshStringAsync(ib).ConfigureAwait(false);

#if !NETFRAMEWORK
                                        keyInts.Add(new BigInteger(bb.ToArray(), isUnsigned: true, isBigEndian: true));
#else
                                        keyInts.Add(new BigInteger(bb.ToArray().Reverse().Concat(new byte[] { 0 }).ToArray()));
#endif
                                    }
                                    _keyInts = keyInts.ToArray();
                                }

                                ByteCollector signPrefix = new(512);
                                signPrefix.Append(new byte[] { (byte)'S', (byte)'S', (byte)'H', (byte)'S', (byte)'I', (byte)'G' });

                                // Namespace
                                bb = await ReadSshStringAsync(bucket).ConfigureAwait(false);
                                signPrefix.Append(NetBitConverter.GetBytes(bb.Length));
                                signPrefix.Append(bb);

                                // Reserved
                                bb = await ReadSshStringAsync(bucket).ConfigureAwait(false);
                                signPrefix.Append(NetBitConverter.GetBytes(bb.Length));
                                signPrefix.Append(bb);

                                // Hash Algorithm
                                bb = await ReadSshStringAsync(bucket).ConfigureAwait(false);
                                signPrefix.Append(NetBitConverter.GetBytes(bb.Length));
                                signPrefix.Append(bb);

                                var sigHashAlgo = (bb).ToASCIIString();
                                switch (sigHashAlgo)
                                {
                                    case "sha256":
                                        _hashAlgorithm = OpenPgpHashAlgorithm.SHA256;
                                        break;
                                    case "sha512":
                                        _hashAlgorithm = OpenPgpHashAlgorithm.SHA512;
                                        break;
                                    default:
                                        throw new NotImplementedException($"Unexpected hash type: {sigHashAlgo} in SSH signature from {bucket.Name} Bucket");
                                }

                                bb = await ReadSshStringAsync(bucket).ConfigureAwait(false);
                                _signature = bb.ToArray();
                                _signBlob = signPrefix.ToArray();

                                using var b = _signature.AsBucket();

                                var tp = await ReadSshStringAsync(b).ConfigureAwait(false);

                                List<BigInteger> bigInts = new();

                                while (!(bb = await ReadSshStringAsync(b).ConfigureAwait(false)).IsEof)
                                {
#if !NETFRAMEWORK
                                    bigInts.Add(new BigInteger(bb.ToArray(), isUnsigned: true, isBigEndian: true));
#else
                                    bigInts.Add(new BigInteger(bb.ToArray().Reverse().Concat(new byte[] { 0 }).ToArray()));
#endif

                                    if (0 == await b.ReadRemainingBytesAsync().ConfigureAwait(false))
                                        break;
                                }

                                _signatureInts = bigInts.ToArray();

                                break;
                            }
                        case OpenPgpTagType.Signature:
                            {
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

                                    int hdrLen = (version == 4) ? 5 : 7;

                                    var bb = await bucket.ReadExactlyAsync(hdrLen).ConfigureAwait(false);

                                    if (bb.Length != hdrLen)
                                        throw new BucketEofException(bucket);

                                    ByteCollector bc = new ByteCollector(1024);
                                    bc.Append(version);
                                    bc.Append(bb);

                                    _signatureType = (OpenPgpSignatureType)bb[0];
                                    _signaturePublicKeyType = (OpenPgpPublicKeyType)bb[1];
                                    _hashAlgorithm = (OpenPgpHashAlgorithm)bb[2];
                                    int subLen;

                                    if (version == 4)
                                        subLen = NetBitConverter.ToUInt16(bb, 3);
                                    else
                                        subLen = (int)NetBitConverter.ToUInt32(bb, 3);

                                    if (subLen > 0)
                                    {
                                        using var subRead = bucket.NoClose().TakeExactly(subLen)
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

                                            var b = await subRead.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(subRead);
                                            len--;

                                            switch ((OpenPgpSubPacketType)b)
                                            {
                                                case OpenPgpSubPacketType.SignatureCreationTime:
                                                    if (len != 4)
                                                        throw new InvalidOperationException();

                                                    var time = await subRead.ReadNetworkInt32Async().ConfigureAwait(false);

                                                    _signTime = DateTimeOffset.FromUnixTimeSeconds(time).DateTime;
                                                    break;
                                                case OpenPgpSubPacketType.Issuer:
                                                    _signer = (await subRead.ReadExactlyAsync((int)len).ConfigureAwait(false)).ToArray();
                                                    break;
                                                case OpenPgpSubPacketType.IssuerFingerprint:
                                                    _fingerPrint = (await subRead.ReadExactlyAsync((int)len).ConfigureAwait(false)).ToArray();
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
                                    _hashStart = await bucket.ReadNetworkUInt16Async().ConfigureAwait(false);

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

                                    _signBlob = bc.ToArray();
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
                                    _signatureType = (OpenPgpSignatureType)bb[1];
                                    _signTime = DateTimeOffset.FromUnixTimeSeconds(NetBitConverter.ToUInt32(bb, 2)).DateTime;
                                    _signer = bb.Slice(6, 8).ToArray();
                                    _signaturePublicKeyType = (OpenPgpPublicKeyType)bb[14];
                                    _hashAlgorithm = (OpenPgpHashAlgorithm)bb[15];
                                    _hashStart = NetBitConverter.ToUInt16(bb, 16);

                                    _signBlob = bb.Slice(1, 5).ToArray();
                                }
                                else
                                    throw new NotImplementedException("Only OpenPGP signature versions 3, 4 and 5 are supported");

                                List<BigInteger> bigInts = new();
                                while (await ReadPgpMultiPrecisionInteger(bucket).ConfigureAwait(false) is BigInteger bi)
                                {
                                    bigInts.Add(bi);
                                }

                                _signatureInts = bigInts.ToArray();
                            }
                            break;
                        case OpenPgpTagType.PublicKey:
                            {
                                var csum = bucket.NoClose().Buffer();
                                uint len = (uint)await csum.ReadRemainingBytesAsync().ConfigureAwait(false);
                                var version = await csum.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(bucket);

                                if (version == 4)
                                {
                                    var bb = await csum.ReadExactlyAsync(5).ConfigureAwait(false);

                                    if (bb.Length != 5)
                                        throw new BucketEofException(bucket);

                                    _keyTime = DateTimeOffset.FromUnixTimeSeconds(NetBitConverter.ToUInt32(bb, 0)).DateTime;
                                    _keyPublicKeyType = (OpenPgpPublicKeyType)bb[4];
                                }
                                else if (version == 3)
                                {
                                    throw new NotImplementedException("Version 3 signature not implemented yet");
                                }
                                else
                                    throw new NotImplementedException("Only OpenPGP public key versions 3 and 4 are supported");

                                List<BigInteger> bigInts = new();
                                while (await ReadPgpMultiPrecisionInteger(csum).ConfigureAwait(false) is BigInteger bi)
                                {
                                    bigInts.Add(bi);
                                }

                                _keyInts = bigInts.ToArray();

                                csum.Reset();

                                await (new byte[] { 0x99 }.AsBucket() + NetBitConverter.GetBytes((ushort)len).AsBucket() + csum).SHA1(x => _keyFingerprint = x).ReadUntilEofAndCloseAsync().ConfigureAwait(false);

                                GC.KeepAlive(_keyFingerprint);
                            }
                            break;
                        default:
                            await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                            break;
                    }
                }
            }
        }

        static async ValueTask<BigInteger?> ReadPgpMultiPrecisionInteger(Bucket sourceData)
        {
            var bb = await sourceData.ReadExactlyAsync(2).ConfigureAwait(false);
            if (bb.IsEof)
                return null;
            else if (bb.Length != 2)
                throw new BucketEofException(sourceData);

            ushort bitLen = NetBitConverter.ToUInt16(bb, 0);

            if (bitLen == 0)
                return new BigInteger(0);
            else
            {
                var byteLen = (bitLen + 7) / 8;
                bb = await sourceData.ReadExactlyAsync(byteLen).ConfigureAwait(false);

                if (bb.Length != byteLen)
                    throw new BucketEofException(sourceData);

                var lst = bb.ToArray();

#if !NETFRAMEWORK
                return new BigInteger(lst, isUnsigned: true, isBigEndian: true);
#else
                // Little endian required.
                Array.Reverse(lst);
                if ((lst[lst.Length - 1] & 0x80) != 0) // Make non-negative
                    lst = lst.ArrayAppend(0);
                return new BigInteger(lst);
#endif
            }
        }

        public async ValueTask<GitPublicKey> ReadKeyAsync()
        {
            await ReadAsync().ConfigureAwait(false);
            return new GitPublicKey(_keyFingerprint!, GetKeyAlgo(_keyPublicKeyType), _keyInts!);
        }

        static GitPublicKeyAlgorithm GetKeyAlgo(OpenPgpPublicKeyType keyPublicKeyType)
            => keyPublicKeyType switch
            {
                OpenPgpPublicKeyType.Rsa => GitPublicKeyAlgorithm.Rsa,
                OpenPgpPublicKeyType.Dsa => GitPublicKeyAlgorithm.Dsa,
                _ => throw new ArgumentOutOfRangeException(nameof(keyPublicKeyType), keyPublicKeyType, null)
            };

        public async ValueTask<bool> VerifyAsync(Bucket sourceData, GitPublicKey? key)
        {
            if (sourceData is null)
                throw new ArgumentNullException(nameof(sourceData));

            await ReadAsync().ConfigureAwait(false);

            byte[] hashValue = null!;

            if (Inner.IsSshSignature)
            {

                await CreateHash(sourceData, x => hashValue = x).ConfigureAwait(false);

                // SSH signature signs blob that contains original hash and some other data
                var toSign = _signBlob!.AsBucket() + NetBitConverter.GetBytes(hashValue.Length).AsBucket() + hashValue.AsBucket();

                OpenPgpHashAlgorithm? overrideAlg = null;

                if (_signaturePublicKeyType == OpenPgpPublicKeyType.Dsa)
                    overrideAlg = OpenPgpHashAlgorithm.SHA1;

                await CreateHash(toSign, x => hashValue = x, overrideAlg: overrideAlg).ConfigureAwait(false);

                if (key is null)
                    return false; // Can't verify SSH signature without key (yet)
            }
            else
            {
                await CreateHash(sourceData + _signBlob!.AsBucket(), x => hashValue = x).ConfigureAwait(false);

                if (NetBitConverter.ToUInt16(hashValue, 0) != _hashStart)
                    return false; // No need to check the actual signature. The hash failed the check

                if (key is null)
                    return true; // Signature is a valid signature, but we can't verify it
            }

            return VerifySignatureAsync(hashValue, key);
        }

        private bool VerifySignatureAsync(byte[] hashValue, GitPublicKey key)
        {
            if (_signatureInts == null)
                throw new InvalidOperationException("No signature value found to verify against");

            switch (_signaturePublicKeyType)
            {
                case OpenPgpPublicKeyType.Rsa:

                    using (RSA rsa = RSA.Create())
                    {
                        byte[] signature = _signatureInts![0].ToByteArray(isUnsigned: true, isBigEndian: true);

                        rsa.ImportParameters(new RSAParameters()
                        {
                            Modulus = key.Values[0].ToByteArray(isUnsigned: true, isBigEndian: true),
                            Exponent = key.Values[1].ToByteArray(isUnsigned: true, isBigEndian: true)
                        });

                        return rsa.VerifyHash(hashValue, signature, GetDotNetHashAlgorithmName(_hashAlgorithm), RSASignaturePadding.Pkcs1);
                    }
                case OpenPgpPublicKeyType.Dsa:
                    using (DSA dsa = DSA.Create())
                    {
                        byte[] signature = _signatureInts![0].ToByteArray(isUnsigned: true, isBigEndian: true);

                        dsa.ImportParameters(new DSAParameters()
                        {
                            P = key.Values[0].ToByteArray(isUnsigned: true, isBigEndian: true),
                            Q = key.Values[1].ToByteArray(isUnsigned: true, isBigEndian: true),
                            G = key.Values[2].ToByteArray(isUnsigned: true, isBigEndian: true),
                            Y = key.Values[3].ToByteArray(isUnsigned: true, isBigEndian: true),
                        });

                        return dsa.VerifySignature(hashValue, signature);
                    }
                case OpenPgpPublicKeyType.Elgamal:
                    // P, G, Y
                    goto default;
                case OpenPgpPublicKeyType.ECDSA:
                    using (var ecdsa = ECDsa.Create())
                    {
                        byte[] signature = _signatureInts!.SelectMany(x => x.ToByteArray(isUnsigned: true, isBigEndian: true)).ToArray();


                        // The name is stored as integer... Nice :(
                        string curveName = Encoding.ASCII.GetString(key.Values[2].ToByteArray(isUnsigned: true, isBigEndian: true));

                        ecdsa.ImportParameters(new ECParameters()
                        {
                            Q = new ECPoint
                            {
                                X = key.Values[0].ToByteArray(isUnsigned: true, isBigEndian: true),
                                Y = key.Values[1].ToByteArray(isUnsigned: true, isBigEndian: true),
                            },
                            Curve = ECCurve.CreateFromFriendlyName(curveName)
                        });

                        return ecdsa.VerifyHash(hashValue, signature);
                    }
                case OpenPgpPublicKeyType.Ed25519:
                    {
                        byte[] signature = _signatureInts!.SelectMany(x => x.ToByteArray(isUnsigned: true, isBigEndian: true)).ToArray();

                        return Cryptographic.Ed25519.CheckValid(signature, hashValue, key.Values[0].ToByteArray(isUnsigned: true, isBigEndian: true));
                    }
                case OpenPgpPublicKeyType.EdDSA:
                default:
                    throw new NotImplementedException($"Signature type {_signaturePublicKeyType} not implemented yet");
            }
        }

        static HashAlgorithmName GetDotNetHashAlgorithmName(OpenPgpHashAlgorithm hashAlgorithm)
            => hashAlgorithm switch
            {
                OpenPgpHashAlgorithm.SHA256 => HashAlgorithmName.SHA256,
                OpenPgpHashAlgorithm.SHA512 => HashAlgorithmName.SHA512,
                OpenPgpHashAlgorithm.SHA384 => HashAlgorithmName.SHA384,
                _ => throw new NotImplementedException($"OpenPGP scheme {hashAlgorithm} not mapped yet.")
            };

        private async ValueTask CreateHash(Bucket sourceData, Action<byte[]> created, OpenPgpHashAlgorithm? overrideAlg = null)
        {
            switch (overrideAlg ?? _hashAlgorithm)
            {
                case OpenPgpHashAlgorithm.SHA256:
                    sourceData = sourceData.SHA256(created);
                    break;
                case OpenPgpHashAlgorithm.SHA512:
                    sourceData = sourceData.SHA512(created);
                    break;
                case OpenPgpHashAlgorithm.SHA384:
                    sourceData = sourceData.SHA384(created);
                    break;
                case OpenPgpHashAlgorithm.SHA1:
                    sourceData = sourceData.SHA1(created);
                    break;
                case OpenPgpHashAlgorithm.MD5:
                    sourceData = sourceData.MD5(created);
                    break;
                case OpenPgpHashAlgorithm.SHA224:
                case OpenPgpHashAlgorithm.MD160:
                    throw new NotImplementedException($"Hash algorithm {_hashAlgorithm} not supported yet");
                default:
                    throw new NotImplementedException($"Hash algorithm {_hashAlgorithm} not supported yet");
            }

            await sourceData.ReadUntilEofAndCloseAsync().ConfigureAwait(false);
        }

        private static async ValueTask<uint?> ReadLengthAsync(Bucket bucket)
        {
            var b = await bucket.ReadByteAsync().ConfigureAwait(false);

            if (!b.HasValue)
                return null;

            if (b < 192)
                return b;

            else if (b < 224)
            {
                var b2 = await bucket.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(bucket);

                return (uint)(((b - 192) << 8) + b2 + 192);
            }
            else if (b == 255)
            {
                return await bucket.ReadNetworkUInt32Async().ConfigureAwait(false);
            }
            else
                throw new NotImplementedException("Partial lengths");
        }

        private static async ValueTask<BucketBytes> ReadSshStringAsync(Bucket bucket)
        {
            int len = (int)await bucket.ReadNetworkUInt32Async().ConfigureAwait(false);

            if (len == 0)
                return BucketBytes.Empty;

            return await bucket.ReadExactlyAsync(len).ConfigureAwait(false);
        }

        internal static ReadOnlyMemory<byte>[] ParseSshStrings(byte[] data)
        {
            List<ReadOnlyMemory<byte>> mems = new();

            // HACK^2: We know we have a memory only bucket, so ignore everything async
            // And we also know the result will refer to the original data, so returning
            // references is safe in this specific edge case.

            var b = data.AsBucket();

            while (b.ReadRemainingBytesAsync().AsTask().Result > 0)
            {
                var bb = ReadSshStringAsync(b).AsTask().Result;

                mems.Add(bb.Memory);
            }

            return mems.ToArray();
        }

        sealed class OpenPgpContainer : WrappingBucket
        {
            bool _notFirst;
            bool _isSsh;
            bool _reading;

            public OpenPgpContainer(Bucket inner) : base(inner)
            {
            }

            public bool IsSshSignature => _isSsh;

            public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
            {
                while (true)
                {
                    var (tag, bucket) = await ReadPacketAsync().ConfigureAwait(false);

                    if (bucket is null)
                        return BucketBytes.Eof;
                }
            }

            public async ValueTask<(Bucket? b, OpenPgpTagType? type)> ReadPacketAsync()
            {
                if (_reading)
                    throw new InvalidOperationException();
                bool first = false;
                Bucket inner = Inner;
                if (!_notFirst)
                {
                    bool didRead = false;
                    var bb = await Inner.PollAsync().ConfigureAwait(false);

                    if (bb.Length < 6)
                    {
                        bb = await Inner.ReadExactlyAsync(6).ConfigureAwait(false);
                        didRead = true;
                    }

                    if (bb.StartsWithASCII("SSHSIG"))
                    {
                        _isSsh = true;
                        if (!didRead)
                            bb = await Inner.ReadExactlyAsync(6).ConfigureAwait(false);
                    }
                    else
                    {
                        if (didRead)
                            inner = bb.ToArray().AsBucket() + Inner;
                    }
                    _notFirst = true;
                    first = true;
                }

                if (_isSsh)
                {
                    if (first)
                    {
                        return (inner.NoClose(), OpenPgpTagType.Signature);
                    }
                    else
                    {
                        await Inner.ReadUntilEofAsync().ConfigureAwait(false);
                        return (null, null);
                    }
                }
                else
                {
                    var bq = await inner.ReadByteAsync().ConfigureAwait(false);

                    if (bq is null)
                        return (null, null);

                    byte b = bq.Value;
                    bool oldFormat;
                    OpenPgpTagType tag;
                    uint remaining = 0;

                    if ((b & 0x80) == 0)
                        throw new BucketException("Bad packet");

                    oldFormat = (0 == (b & 0x40));
                    if (oldFormat)
                    {
                        tag = (OpenPgpTagType)((b & 0x3c) >> 2);
                        remaining = (uint)(b & 0x3);
                    }
                    else
                        tag = (OpenPgpTagType)(b & 0x2F);

                    if (!oldFormat)
                    {
                        uint len = await ReadLengthAsync(inner).ConfigureAwait(false) ?? throw new BucketEofException(Inner);

                        remaining = len;
                    }
                    else
                    {
                        switch (remaining)
                        {
                            case 0:
                                remaining = await inner.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(Inner);
                                break;
                            case 1:
                                remaining = await inner.ReadNetworkUInt16Async().ConfigureAwait(false);
                                break;
                            case 2:
                                remaining = await inner.ReadNetworkUInt32Async().ConfigureAwait(false);
                                break;
                            default:
                                throw new NotImplementedException("Indetermined size");
                        }
                    }

                    _reading = true;
                    return (inner.NoClose().TakeExactly(remaining).AtEof(() => _reading = false), tag);
                }
            }
        }
    }
}
