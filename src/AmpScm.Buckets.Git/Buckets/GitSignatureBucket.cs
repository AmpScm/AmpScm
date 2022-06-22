using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git;
using AmpScm.Buckets.Specialized;
using System.Globalization;

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
        SHA224 = 11,
        SHA256v3 = 12,
        // 13 is reserved
        SHA512v3 = 14,
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
        private ReadOnlyMemory<byte>[]? _signatureInts;
        private DateTime _keyTime;
        private OpenPgpPublicKeyType _keyPublicKeyType;
        private ReadOnlyMemory<byte>[]? _keyInts;
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
                        case OpenPgpTagType.Signature when Inner.IsSshSignature && (_signatureInts is null):
                            {
                                uint sshVersion = await bucket.ReadNetworkUInt32Async().ConfigureAwait(false);

                                if (sshVersion != 1)
                                    throw new BucketException();

                                var bb = await ReadSshStringAsync(bucket).ConfigureAwait(false);
                                _keyFingerprint = bb.ToArray();

                                using (var ib = _keyFingerprint.AsBucket())
                                {
                                    bb = await ReadSshStringAsync(ib).ConfigureAwait(false);
                                    string alg = bb.ToASCIIString();

                                    if (alg.StartsWith("sk-", StringComparison.Ordinal))
                                        alg = alg.Substring(3);

                                    _signaturePublicKeyType = alg switch
                                    {
                                        "ssh-rsa" => OpenPgpPublicKeyType.Rsa,
                                        "ssh-dss" => OpenPgpPublicKeyType.Dsa,
                                        "ssh-ed25519" or "ssh-ed25519@openssh.com" => OpenPgpPublicKeyType.Ed25519,
                                        "ecdsa-sha2-nistp256" or "ecdsa-sha2-nistp384" or "ecdsa-sha2-nistp521" => OpenPgpPublicKeyType.ECDSA,
                                        _ => throw new NotImplementedException($"Unknown public key type: {alg}"),
                                    };

                                    List<ReadOnlyMemory<byte>> keyInts = new();
                                    while (!(bb = await ReadSshStringAsync(ib).ConfigureAwait(false)).IsEof)
                                    {
                                        keyInts.Add(bb.ToArray());
                                    }

                                    _keyInts = keyInts.ToArray();

                                    if (_signaturePublicKeyType == OpenPgpPublicKeyType.ECDSA)
                                        _keyInts = GetEcdsaValues(_keyInts);
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

                                _hashAlgorithm = sigHashAlgo switch
                                {
                                    "sha256" => OpenPgpHashAlgorithm.SHA256,
                                    "sha512" => OpenPgpHashAlgorithm.SHA512,
                                    _ => throw new NotImplementedException($"Unexpected hash type: {sigHashAlgo} in SSH signature from {bucket.Name} Bucket"),
                                };

                                bb = await ReadSshStringAsync(bucket).ConfigureAwait(false);
                                _signature = bb.ToArray();
                                _signBlob = signPrefix.ToArray();

                                using var b = _signature.AsBucket();

                                var tp = await ReadSshStringAsync(b).ConfigureAwait(false);

                                List<ReadOnlyMemory<byte>> bigInts = new();
                                int i = 0;
                                while (!(bb = await ReadSshStringAsync(b).ConfigureAwait(false)).IsEof)
                                {
                                    if (SplitSignatureInt(i++, _signaturePublicKeyType))
                                    {
                                        var s = bb.ToArray().AsBucket();

                                        while (!(bb = await ReadSshStringAsync(s).ConfigureAwait(false)).IsEof)
                                        {
                                            bigInts.Add(bb.ToArray());
                                        }

                                        continue;
                                    }

                                    bigInts.Add(bb.ToArray());
                                }

                                _signatureInts = bigInts.ToArray();

                                break;
                            }
                        case OpenPgpTagType.Signature when (_signatureInts is null):
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
                                                    _keyFingerprint = (await subRead.ReadExactlyAsync((int)len).ConfigureAwait(false)).ToArray();
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

                                List<ReadOnlyMemory<byte>> bigInts = new();
                                while (await ReadPgpMultiPrecisionInteger(bucket).ConfigureAwait(false) is ReadOnlyMemory<byte> bi)
                                {
                                    bigInts.Add(bi);
                                }

                                _signatureInts = bigInts.ToArray();

                                if (_signaturePublicKeyType == OpenPgpPublicKeyType.EdDSA && _signatureInts.Length == 2 && _signatureInts.All(x => x.Length == 32))
                                {
                                    _signaturePublicKeyType = OpenPgpPublicKeyType.Ed25519;
                                    _signatureInts = new ReadOnlyMemory<byte>[] { _signatureInts.SelectMany(x => x.ToArray()).ToArray() };
                                }
                            }
                            break;
                        case OpenPgpTagType.PublicKey when (_keyInts is null):
                            {
                                var csum = bucket.NoClose().Buffer();
                                uint len = (uint)await csum.ReadRemainingBytesAsync().ConfigureAwait(false);
                                var version = await csum.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(bucket);

                                if (version == 4 || version == 5)
                                {
                                    var bb = await csum.ReadExactlyAsync(5).ConfigureAwait(false);

                                    if (bb.Length != 5)
                                        throw new BucketEofException(bucket);

                                    _keyTime = DateTimeOffset.FromUnixTimeSeconds(NetBitConverter.ToUInt32(bb, 0)).DateTime;
                                    _keyPublicKeyType = (OpenPgpPublicKeyType)bb[4];

                                    if (version == 5)
                                        await csum.ReadNetworkInt32Async().ConfigureAwait(false); // Length of what follows
                                }
                                else if (version == 3)
                                {
                                    throw new NotImplementedException("Version 3 signature not implemented yet");
                                }
                                else
                                    throw new NotImplementedException("Only OpenPGP public key versions 3, 4 and 5 are supported");

                                List<ReadOnlyMemory<byte>> bigInts = new();

                                if (_keyPublicKeyType is OpenPgpPublicKeyType.EdDSA or OpenPgpPublicKeyType.ECDSA)
                                {
                                    var b = await csum.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(csum);

                                    if (b == 0 || b == 0xFF)
                                        throw new NotImplementedException("Reserved value");

                                    var bb = await csum.ReadExactlyAsync(b).ConfigureAwait(false);

                                    bigInts.Add(bb.ToArray());
                                }

                                while (await ReadPgpMultiPrecisionInteger(csum).ConfigureAwait(false) is ReadOnlyMemory<byte> bi)
                                {
                                    bigInts.Add(bi);
                                }

                                csum.Reset();

                                await (new byte[] { 0x99 }.AsBucket() + NetBitConverter.GetBytes((ushort)len).AsBucket() + csum).SHA1(x => _keyFingerprint = x).ReadUntilEofAndCloseAsync().ConfigureAwait(false);



                                _keyInts = bigInts.ToArray();

                                if (_keyPublicKeyType == OpenPgpPublicKeyType.ECDSA)
                                {
                                    _keyInts = GetEcdsaValues(_keyInts, true);
                                }
                                else if (_keyPublicKeyType == OpenPgpPublicKeyType.EdDSA && _keyInts[0].Span.SequenceEqual(new byte[] { 0x2B, 0x06, 0x01, 0x04, 0x01, 0xDA, 0x47, 0x0F, 0x01 }))
                                {
                                    // This algorithm is not implemented by .Net, but for this specific curve we have a workaround
                                    _keyPublicKeyType = OpenPgpPublicKeyType.Ed25519;
                                    // Convert `0x40 | value` form to `value`
                                    _keyInts = new ReadOnlyMemory<byte>[] { _keyInts[1].Slice(1).ToArray() };
                                }
                            }
                            break;
                        default:
                            await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                            break;
                    }
                }
            }
        }

        static bool SplitSignatureInt(int index, OpenPgpPublicKeyType signaturePublicKeyType)
        {
            return signaturePublicKeyType == OpenPgpPublicKeyType.ECDSA && index == 0;
        }

        static async ValueTask<ReadOnlyMemory<byte>?> ReadPgpMultiPrecisionInteger(Bucket sourceData)
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
                var byteLen = (bitLen + 7) / 8;
                bb = await sourceData.ReadExactlyAsync(byteLen).ConfigureAwait(false);

                if (bb.Length != byteLen)
                    throw new BucketEofException(sourceData);

                return bb.ToArray();
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
                OpenPgpPublicKeyType.EdDSA => GitPublicKeyAlgorithm.EdDsa,
                OpenPgpPublicKeyType.ECDSA => GitPublicKeyAlgorithm.Ecdsa,
                OpenPgpPublicKeyType.Ed25519 => GitPublicKeyAlgorithm.Ed25519,
                _ => throw new ArgumentOutOfRangeException(nameof(keyPublicKeyType), keyPublicKeyType, null)
            };

        public async ValueTask<ReadOnlyMemory<byte>> ReadFingerprintAsync()
        {
            await ReadAsync().ConfigureAwait(false);

            return _keyFingerprint;
        }

        public async ValueTask<bool> VerifyAsync(Bucket sourceData, GitPublicKey? key)
        {
            if (sourceData is null)
                throw new ArgumentNullException(nameof(sourceData));

            await ReadAsync().ConfigureAwait(false);

            byte[] hashValue = null!;

            if (Inner.IsSshSignature)
            {

                await CreateHash(sourceData, x => hashValue = x, _hashAlgorithm).ConfigureAwait(false);

                // SSH signature signs blob that contains original hash and some other data
                var toSign = _signBlob!.AsBucket() + NetBitConverter.GetBytes(hashValue.Length).AsBucket() + hashValue.AsBucket();

                OpenPgpHashAlgorithm? overrideAlg = null;

                if (_signaturePublicKeyType == OpenPgpPublicKeyType.Dsa)
                    overrideAlg = OpenPgpHashAlgorithm.SHA1;
                else if (_signaturePublicKeyType == OpenPgpPublicKeyType.ECDSA)
                {
                    string curveName = Encoding.ASCII.GetString((key?.Values[0] ?? _keyInts![2]).ToArray());

                    overrideAlg = OpenPgpHashAlgorithm.SHA512;

                    if (curveName.EndsWith("256", StringComparison.Ordinal) || curveName.EndsWith("256r1", StringComparison.Ordinal))
                        overrideAlg = OpenPgpHashAlgorithm.SHA256;
                    else if (curveName.EndsWith("384", StringComparison.Ordinal))
                        overrideAlg = OpenPgpHashAlgorithm.SHA384;
                    else if (curveName.EndsWith("521", StringComparison.Ordinal))
                        overrideAlg = OpenPgpHashAlgorithm.SHA512;
                }
                else if (_signaturePublicKeyType == OpenPgpPublicKeyType.Rsa && Inner.IsSshSignature)
                    overrideAlg = OpenPgpHashAlgorithm.SHA512;

                if (_signaturePublicKeyType != OpenPgpPublicKeyType.Ed25519) // Ed25519 doesn't use a second hash
                    await CreateHash(toSign, x => hashValue = x, overrideAlg ?? _hashAlgorithm).ConfigureAwait(false);
                else
                    hashValue = toSign.ToArray();

                if (key is null && _keyInts is null)
                    return false; // Can't verify SSH signature without key (yet)
            }
            else
            {
                await CreateHash(sourceData + _signBlob!.AsBucket(), x => hashValue = x, _hashAlgorithm).ConfigureAwait(false);

                if (NetBitConverter.ToUInt16(hashValue, 0) != _hashStart)
                    return false; // No need to check the actual signature. The hash failed the check

                if (key is null)
                    return true; // Signature is a valid signature, but we can't verify it
            }

            var v = key?.Values ?? _keyInts ?? throw new InvalidOperationException("No key to verify with");

            return VerifySignatureAsync(hashValue, v);
        }

        private bool VerifySignatureAsync(byte[] hashValue, IReadOnlyList<ReadOnlyMemory<byte>> keyValues)
        {
            if (_signatureInts == null)
                throw new InvalidOperationException("No signature value found to verify against");

            switch (_signaturePublicKeyType)
            {
                case OpenPgpPublicKeyType.Rsa:

                    using (RSA rsa = RSA.Create())
                    {
                        byte[] signature = _signatureInts![0].ToArray();

                        rsa.ImportParameters(new RSAParameters()
                        {
                            Modulus = MakeUnsignedArray(keyValues[0]),
                            Exponent = MakeUnsignedArray(keyValues[1])
                        });

                        return rsa.VerifyHash(hashValue, signature, GetDotNetHashAlgorithmName(_hashAlgorithm), RSASignaturePadding.Pkcs1);
                    }
                case OpenPgpPublicKeyType.Dsa:
                    using (DSA dsa = DSA.Create())
                    {
                        byte[] signature = _signatureInts![0].ToArray();

                        dsa.ImportParameters(new DSAParameters()
                        {
                            P = MakeUnsignedArray(keyValues[0], 4),
                            Q = MakeUnsignedArray(keyValues[1], 4),
                            G = MakeUnsignedArray(keyValues[2], 4),
                            Y = MakeUnsignedArray(keyValues[3], 4)
                        });

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
                        byte[] r = MakeUnsignedArray(_signatureInts[0]);
                        byte[] s = MakeUnsignedArray(_signatureInts[1]);

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
                        byte[] signature = _signatureInts![0].ToArray();

                        return Chaos.NaCl.Ed25519.Verify(signature, hashValue, keyValues[0].ToArray());
                        //return Cryptographic.Ed25519.CheckValid(signature, hashValue, keyValues[0].ToArray());
                    }
                case OpenPgpPublicKeyType.EdDSA:
                default:
                    throw new NotImplementedException($"Public Key type {_signaturePublicKeyType} not implemented yet");
            }
        }

        static byte[] MakeUnsignedArray(ReadOnlyMemory<byte> readOnlyMemory, int? makeLen = null)
        {
            if (readOnlyMemory.Span[0] == 0 && readOnlyMemory.Length != makeLen)
                return readOnlyMemory.Slice(1).ToArray();
            else if (makeLen > readOnlyMemory.Length)
            {
                byte[] result = new byte[makeLen.Value];
                readOnlyMemory.CopyTo(result.AsMemory(result.Length - readOnlyMemory.Length, readOnlyMemory.Length));
                return result;
            }
            else
                return readOnlyMemory.ToArray();
        }

        static HashAlgorithmName GetDotNetHashAlgorithmName(OpenPgpHashAlgorithm hashAlgorithm)
            => hashAlgorithm switch
            {
                OpenPgpHashAlgorithm.SHA256 => HashAlgorithmName.SHA256,
                OpenPgpHashAlgorithm.SHA512 => HashAlgorithmName.SHA512,
                OpenPgpHashAlgorithm.SHA384 => HashAlgorithmName.SHA384,
                _ => throw new NotImplementedException($"OpenPGP scheme {hashAlgorithm} not mapped yet.")
            };

        internal static ReadOnlyMemory<byte>[] GetEcdsaValues(IEnumerable<ReadOnlyMemory<byte>> vals, bool pgp = false)
        {
            ReadOnlyMemory<byte>[] signature;

            var curve = vals.First();
            var v2 = vals.Skip(1).First().ToArray();

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
                // These 2 are only used with Eddsa
                //else if (curve.Span.SequenceEqual(new byte[] { 0x2B, 0x06, 0x01, 0x04, 0x01, 0xDA, 0x47, 0x0F, 0x01 }))
                //    curveName = "Ed25519";
                //else if (curve.Span.SequenceEqual(new byte[] { 0x2B, 0x06, 0x01, 0x04, 0x01, 0x97, 0x55, 0x01, 0x05, 0x01 }))
                //    curveName = "Curve25519";
                else
                    throw new NotImplementedException("Unknown curve oid in ecdsa key");

                curve = Encoding.ASCII.GetBytes(curveName);
            }

            switch (v2[0])
            {
                case 2: // Y is even
                case 3: // Y is odd
                default:
                    // TODO: Find some implementation to calculate X from Y
                    throw new NotImplementedException("Only X and Y follow format is supported at this time");
                case 4: // X and Y follow
                        // X and Y both have the same number of bits... Half the value
                    signature = new[]
                                {
                                    // The Curve name is stored as integer... Nice :(.. But at least consistent
                                    curve,

                                    v2.Skip(1).Take(v2.Length / 2).ToArray(),
                                    v2.Skip(1 + v2.Length / 2).Take(v2.Length / 2).ToArray(),
                                };
                    break;
                case 0x40 when (pgp): // Custom compressed poing see rfc4880bis-06
                    signature = new[]
                                {
                                    curve,

                                    v2.Skip(1).ToArray(),
                                };
                    break;

            }

            return signature;
        }

        static async ValueTask CreateHash(Bucket sourceData, Action<byte[]> created, OpenPgpHashAlgorithm hashAlgorithm)
        {
            sourceData = hashAlgorithm switch
            {
                OpenPgpHashAlgorithm.SHA256 => sourceData.SHA256(created),
                OpenPgpHashAlgorithm.SHA512 => sourceData.SHA512(created),
                OpenPgpHashAlgorithm.SHA384 => sourceData.SHA384(created),
                OpenPgpHashAlgorithm.SHA1 => sourceData.SHA1(created),
                OpenPgpHashAlgorithm.MD5 => sourceData.MD5(created),
#if NETFRAMEWORK
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
                OpenPgpHashAlgorithm.MD160 => sourceData.Hash(RIPEMD160.Create(), created),
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
#else
                OpenPgpHashAlgorithm.MD160 or
#endif
                OpenPgpHashAlgorithm.SHA224 => throw new NotImplementedException($"Hash algorithm {hashAlgorithm} not supported yet"),
                _ => throw new NotImplementedException($"Hash algorithm {hashAlgorithm} not supported yet"),
            };
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

        internal static ReadOnlyMemory<byte>[] ParseSshStrings(byte[] data)
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

        public static string FingerprintToString(IReadOnlyList<byte> fingerprint)
        {
            if (fingerprint is null || fingerprint.Count == 0)
                throw new ArgumentNullException(nameof(fingerprint));

            var b0 = fingerprint[0];

            if (b0 >= 3 && b0 <= 5) // OpenPgp fingeprint formats 3-5
                return string.Join("", fingerprint.Skip(1).Select(x => x.ToString("X2", CultureInfo.InvariantCulture)));
            else if (b0 == 0 && fingerprint[1] == 0 && fingerprint[2] == 0)
            {
                var bytes = fingerprint as byte[] ?? fingerprint.ToArray();
                var vals = GitSignatureBucket.ParseSshStrings(bytes);

                return $"{Encoding.ASCII.GetString(vals[0].ToArray())} {Convert.ToBase64String(bytes)}";
            }

            return "";
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
                    var (_, bucket) = await ReadPacketAsync().ConfigureAwait(false);

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
                        remaining = remaining switch
                        {
                            0 => await inner.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(Inner),
                            1 => await inner.ReadNetworkUInt16Async().ConfigureAwait(false),
                            2 => await inner.ReadNetworkUInt32Async().ConfigureAwait(false),
                            _ => throw new NotImplementedException("Indetermined size"),
                        };
                    }

                    _reading = true;
                    return (inner.NoClose().TakeExactly(remaining).AtEof(() => _reading = false), tag);
                }
            }
        }
    }
}
