using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;
using System.Globalization;
using AmpScm.Buckets;

namespace AmpScm.Buckets.Signatures
{

    /// <summary>
    /// Reads an OpenPGP or OpenSSH signature
    /// </summary>
    public sealed class SignatureBucket : WrappingBucket
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
        readonly List<SignatureBucketKey> _keys = new();
        byte[]? _signKeyFingerprint;
        readonly Bucket _outer;

        new OpenPgpContainer Inner => (OpenPgpContainer)base.Inner;

        public SignatureBucket(Bucket inner)
            : base(new OpenPgpContainer(inner))
        {
            _outer = inner;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
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

                if (bucket is null)
                    return;

                using (bucket)
                {
                    switch (tag)
                    {
                        case OpenPgpTagType.Signature when Inner.IsSsh && _signatureInts is null:
                            {
                                var sshVersion = await bucket.ReadNetworkUInt32Async().ConfigureAwait(false);

                                if (sshVersion != 1)
                                    throw new BucketException();

                                var bb = await ReadSshStringAsync(bucket).ConfigureAwait(false);
                                byte[] keyFingerprint = bb.ToArray();
                                ReadOnlyMemory<byte>[]? keyInts;

                                using (var ib = keyFingerprint.AsBucket())
                                {
                                    bb = await ReadSshStringAsync(ib).ConfigureAwait(false);
                                    var alg = bb.ToASCIIString();

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

                                    List<ReadOnlyMemory<byte>> keyList = new();
                                    while (!(bb = await ReadSshStringAsync(ib).ConfigureAwait(false)).IsEof)
                                    {
                                        keyList.Add(bb.ToArray());
                                    }

                                    keyInts = keyList.ToArray();

                                    if (_signaturePublicKeyType == OpenPgpPublicKeyType.ECDSA)
                                        keyInts = GetEcdsaValues(keyInts);
                                }

                                _signKeyFingerprint ??= keyFingerprint;
                                _keys.Add(new SignatureBucketKey(keyFingerprint!, GetKeyAlgo(_signaturePublicKeyType), keyInts));

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

                                var sigHashAlgo = bb.ToASCIIString();

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
                                var i = 0;
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
                        case OpenPgpTagType.PublicKey when Inner.IsSsh:
                            {
                                BucketBytes bb;
                                ByteCollector bc = new();
                                string? sshKeyType = null;

                                while (!(bb = await ReadSshStringAsync(bucket).ConfigureAwait(false)).IsEof)
                                {
                                    if (sshKeyType == null)
                                        sshKeyType = bb.ToASCIIString();

                                    bc.Append(NetBitConverter.GetBytes(bb.Length));
                                    bc.Append(bb);
                                }

                                var k = $"{sshKeyType} {Convert.ToBase64String(bc.ToArray())} bb";

                                if (SignatureBucketKey.TryParseSshLine(k, out var kk))
                                {
                                    _keys.Add(kk);
                                }
                                break;
                            }
                        case OpenPgpTagType.Signature when _signatureInts is null:
                            {
                                var version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(bucket);

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

                                    var hdrLen = version == 4 ? 5 : 7;

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
                                                    _signKeyFingerprint = (await subRead.ReadExactlyAsync((int)len).ConfigureAwait(false)).ToArray();
                                                    break;

                                                // Currently unhandled fields from private keys
                                                case OpenPgpSubPacketType.KeyFlags:
                                                case OpenPgpSubPacketType.KeyExpirationTime:
                                                case OpenPgpSubPacketType.PreferredSymetricAlgorithms:
                                                case OpenPgpSubPacketType.PreferredHashAlgorithms:
                                                case OpenPgpSubPacketType.PreferredCompressionAlgorithms:
                                                case OpenPgpSubPacketType.Features:
                                                case OpenPgpSubPacketType.KeyServerPreferences:
                                                case (OpenPgpSubPacketType)34: // ??
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
                                else if (_signaturePublicKeyType == OpenPgpPublicKeyType.Dsa)
                                {
                                    _signatureInts = new ReadOnlyMemory<byte>[] { _signatureInts.SelectMany(x => x.ToArray()).ToArray() };
                                }
                            }
                            break;
                        case OpenPgpTagType.PublicKey:
                        case OpenPgpTagType.PublicSubkey:
                        case OpenPgpTagType.SecretKey:
                        case OpenPgpTagType.SecretSubkey:
                            {
                                DateTime keyTime;
                                ReadOnlyMemory<byte>[]? keyInts;
                                byte[]? keyFingerprint = null;
                                OpenPgpPublicKeyType keyPublicKeyType;
                                bool hasSecretKey = (tag is OpenPgpTagType.SecretKey or OpenPgpTagType.SecretSubkey);

                                var csum = bucket.NoDispose().Buffer();
                                var len = (uint)await csum.ReadRemainingBytesAsync().ConfigureAwait(false);
                                var version = await csum.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(bucket);

                                if (version == 4 || version == 5)
                                {
                                    var bb = await csum.ReadExactlyAsync(5).ConfigureAwait(false);

                                    if (bb.Length != 5)
                                        throw new BucketEofException(bucket);

                                    keyTime = DateTimeOffset.FromUnixTimeSeconds(NetBitConverter.ToUInt32(bb, 0)).DateTime;
                                    keyPublicKeyType = (OpenPgpPublicKeyType)bb[4];

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

                                int nrOfInts = keyPublicKeyType switch
                                {
                                    OpenPgpPublicKeyType.Rsa or
                                    OpenPgpPublicKeyType.RsaEncryptOnly or
                                    OpenPgpPublicKeyType.RsaSignOnly => 2,
                                    OpenPgpPublicKeyType.Elgamal => 3,
                                    OpenPgpPublicKeyType.Dsa => 4,
                                    OpenPgpPublicKeyType.ECDH => 3,
                                    OpenPgpPublicKeyType.ECDSA => 2,
                                    OpenPgpPublicKeyType.EdDSA => 2,
                                    _ => throw new NotImplementedException($"Unexpected public key type {keyPublicKeyType}")
                                };
                                if (keyPublicKeyType is OpenPgpPublicKeyType.EdDSA or OpenPgpPublicKeyType.ECDSA or OpenPgpPublicKeyType.ECDH)
                                {
                                    var b = await csum.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(csum);

                                    if (b == 0 || b == 0xFF)
                                        throw new NotImplementedException("Reserved value");

                                    var bb = await csum.ReadExactlyAsync(b).ConfigureAwait(false);
                                    if (bb.Length != b)
                                        throw new BucketEofException(bucket);

                                    bigInts.Add(bb.ToArray());
                                }

                                if (keyPublicKeyType is OpenPgpPublicKeyType.ECDH)
                                {
                                    var bi = await ReadPgpMultiPrecisionInteger(csum).ConfigureAwait(false);

                                    bigInts.Add(bi.Value);

                                    var b = await csum.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(csum);

                                    if (b == 0 || b == 0xFF)
                                        throw new NotImplementedException("Reserved value");

                                    var bb = await csum.ReadExactlyAsync(b).ConfigureAwait(false);
                                    if (bb.Length != b)
                                        throw new BucketEofException(bucket);

                                    bigInts.Add(bb.ToArray());
                                }


                                while (bigInts.Count < nrOfInts && await ReadPgpMultiPrecisionInteger(csum).ConfigureAwait(false) is ReadOnlyMemory<byte> bi)
                                {
                                    bigInts.Add(bi);
                                }

                                if (nrOfInts > bigInts.Count)
                                {
                                    throw new NotImplementedException($"Nr of ints for {keyPublicKeyType} should be {bigInts.Count}");
                                }
                                keyInts = bigInts.ToArray();

                                csum.Reset();

                                if (version == 4)
                                {
                                    await (new byte[] { 0x99 }.AsBucket() + NetBitConverter.GetBytes((ushort)len).AsBucket() + csum)
                                        .SHA1(x => keyFingerprint = x)
                                        .ReadUntilEofAndCloseAsync().ConfigureAwait(false);
                                }
                                else if (version == 5)
                                {
                                    await (new byte[] { 0x9A }.AsBucket() + NetBitConverter.GetBytes(len).AsBucket() + csum)
                                        .SHA256(x => keyFingerprint = x)
                                        .ReadUntilEofAndCloseAsync().ConfigureAwait(false);
                                }
                                //else if (version == 3)
                                //{
                                //    await bigInts.SelectMany(x => x.ToArray()).ToArray().AsBucket()
                                //        .MD5(x => keyFingerprint = x)
                                //        .ReadUntilEofAndCloseAsync().ConfigureAwait(false);
                                //}

                                keyFingerprint = new byte[] { version }.Concat(keyFingerprint!).ToArray();

                                if (keyPublicKeyType == OpenPgpPublicKeyType.ECDSA)
                                {
                                    keyInts = GetEcdsaValues(keyInts, true);
                                }
                                else if (keyPublicKeyType == OpenPgpPublicKeyType.EdDSA && keyInts[0].Span.SequenceEqual(new byte[] { 0x2B, 0x06, 0x01, 0x04, 0x01, 0xDA, 0x47, 0x0F, 0x01 }))
                                {
                                    // This algorithm is not implemented by .Net, but for this specific curve we have a workaround
                                    keyPublicKeyType = OpenPgpPublicKeyType.Ed25519;
                                    // Convert `0x40 | value` form to `value`
                                    keyInts = new ReadOnlyMemory<byte>[] { keyInts[1].Slice(1).ToArray() };
                                }
                                else if (keyPublicKeyType == OpenPgpPublicKeyType.ECDH && keyInts[0].Span.SequenceEqual(new byte[] { 0x2B, 0x06, 0x01, 0x04, 0x01, 0x97, 0x55, 0x01, 0x05, 0x01 }))
                                {
                                    keyPublicKeyType = OpenPgpPublicKeyType.Curve25519;
                                    keyInts = keyInts.Skip(1).ToArray();
                                }

                                if (hasSecretKey)
                                {
                                    await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                                }

                                _keys.Add(new SignatureBucketKey(keyFingerprint!, GetKeyAlgo(keyPublicKeyType), keyInts));
                            }
                            break;
                        case OpenPgpTagType.DerValue:
                            {
                                var db = (DerBucket)bucket;
                                var (derRoot, t) = await db.ReadValueAsync().ConfigureAwait(false);

                                var pkt = (_outer as Radix64ArmorBucket)?.PublicKeyType;

                                if (t != DerType.Sequence || pkt is null)
                                    throw new BucketException("Unexpected DER value");

                                using var der = new DerBucket(derRoot!);


                                BucketBytes bb = new(pkt.Value);

                                if (bb.StartsWithASCII("RSA"))
                                {
                                    List<ReadOnlyMemory<byte>> vals = new();

                                    var (b, bt) = await der.ReadValueAsync().ConfigureAwait(false);
                                    if (b is null)
                                        throw new BucketEofException(der);

                                    bb = await b.ReadExactlyAsync(8192).ConfigureAwait(false);
                                    vals.Add(bb.ToArray());

                                    (b, bt) = await der.ReadValueAsync().ConfigureAwait(false);
                                    if (b is null)
                                        throw new BucketEofException(der);

                                    bb = await b.ReadExactlyAsync(8192).ConfigureAwait(false);
                                    vals.Add(bb.ToArray());

                                    var keyInts = vals.ToArray();

                                    _keys.Add(new SignatureBucketKey(CreateSshFingerprint(SignatureBucketAlgorithm.Rsa, keyInts), SignatureBucketAlgorithm.Rsa, keyInts));
                                }
                                else if (bb.IsEmpty)
                                {
                                    var (ob, obt) = await der.ReadValueAsync().ConfigureAwait(false);

                                    if (obt != DerType.Sequence)
                                    {
                                        await der.ReadUntilEofAsync().ConfigureAwait(false);
                                        break;
                                    }

                                    using var der2 = new DerBucket(ob!);

                                    (ob, obt) = await der2.ReadValueAsync().ConfigureAwait(false);

                                    bb = await ob!.ReadExactlyAsync(32).ConfigureAwait(false);

                                    SignatureBucketAlgorithm sba;

                                    if (bb.Span.SequenceEqual(new byte[] { 0x2a, 0x86, 0x48, 0xce, 0x38, 0x04, 0x01 }))
                                        sba = SignatureBucketAlgorithm.Dsa;
                                    else if (bb.Span.SequenceEqual(new byte[] { 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x02, 0x01 }))
                                        sba = SignatureBucketAlgorithm.Ecdsa;
                                    else
                                    {
                                        await ob!.ReadUntilEofAsync().ConfigureAwait(false);
                                        await der2.ReadUntilEofAsync().ConfigureAwait(false);
                                        await der.ReadUntilEofAsync().ConfigureAwait(false);
                                        break;
                                    }

                                    List<ReadOnlyMemory<byte>> vals = new();

                                    await SequenceToList(vals, der2).ConfigureAwait(false);
                                    await SequenceToList(vals, der).ConfigureAwait(false);

                                    ReadOnlyMemory<byte>[] keyInts = vals.ToArray();

                                    if (sba == SignatureBucketAlgorithm.Ecdsa && vals.Count >= 2)
                                    {
                                        vals[1] = vals[1].Slice(1).ToArray();
                                        keyInts = GetEcdsaValues(vals, true);
                                    }
                                    else
                                        keyInts = vals.ToArray();

                                    _keys.Add(new SignatureBucketKey(CreateSshFingerprint(sba, keyInts), sba, keyInts));
                                }
                                else
                                    await der.ReadUntilEofAsync().ConfigureAwait(false);
                            }
                            break;
                        case OpenPgpTagType.UserID:
                        case OpenPgpTagType.Signature:
                            await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                            break;
                        default:
                            await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                            break;
                    }
                }
            }
        }

        static ReadOnlyMemory<byte> CreateSshFingerprint(SignatureBucketAlgorithm sba, ReadOnlyMemory<byte>[] keyInts)
        {
            ByteCollector bb = new(4096);

            var ints = keyInts.ToList();

            string alg;
            switch (sba)
            {
                case SignatureBucketAlgorithm.Rsa:
                    alg = "ssh-rsa";
                    (ints[1], ints[0]) = (ints[0], ints[1]);
                    break;
                case SignatureBucketAlgorithm.Dsa:
                    alg = "ssh-dss";
                    break;
                case SignatureBucketAlgorithm.Ed25519:
                    alg = "ssh-ed25519";
                    break;
                case SignatureBucketAlgorithm.Ecdsa:
                    string kv = Encoding.ASCII.GetString(keyInts[0].ToArray());
                    alg = "ecdsa-sha2-" + kv.ToString();

                    ints[1] = new byte[] { 4 }.Concat(ints[1].ToArray()).Concat(ints[2].ToArray()).ToArray();
                    ints.RemoveAt(2);
                    break;
                default:
                    throw new NotImplementedException();
            }

            bb.Append(NetBitConverter.GetBytes(alg.Length));
            bb.Append(Encoding.ASCII.GetBytes(alg));

            for (int i = 0; i < ints.Count; i++)
            {
                bb.Append(NetBitConverter.GetBytes(ints[i].Length));
                bb.Append(ints[i]);
            }

            return bb.ToArray();
        }

        static async ValueTask SequenceToList(List<ReadOnlyMemory<byte>> vals, DerBucket der2)
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

            var bitLen = NetBitConverter.ToUInt16(bb, 0);

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

        public async ValueTask<SignatureBucketKey> ReadKeyAsync()
        {
            await ReadAsync().ConfigureAwait(false);
            return _keys.First();
        }

        static SignatureBucketAlgorithm GetKeyAlgo(OpenPgpPublicKeyType keyPublicKeyType)
            => keyPublicKeyType switch
            {
                OpenPgpPublicKeyType.Rsa => SignatureBucketAlgorithm.Rsa,
                OpenPgpPublicKeyType.Dsa => SignatureBucketAlgorithm.Dsa,
                OpenPgpPublicKeyType.ECDSA => SignatureBucketAlgorithm.Ecdsa,
                OpenPgpPublicKeyType.Ed25519 => SignatureBucketAlgorithm.Ed25519,
                OpenPgpPublicKeyType.ECDH => SignatureBucketAlgorithm.Ecdh,
                OpenPgpPublicKeyType.Curve25519 => SignatureBucketAlgorithm.Curve25519,
                OpenPgpPublicKeyType.Elgamal => SignatureBucketAlgorithm.Elgamal,
                _ => throw new ArgumentOutOfRangeException(nameof(keyPublicKeyType), keyPublicKeyType, null)
            };

        public async ValueTask<ReadOnlyMemory<byte>> ReadFingerprintAsync()
        {
            await ReadAsync().ConfigureAwait(false);

            return _signKeyFingerprint;
        }

        public async ValueTask<bool> VerifyAsync(Bucket sourceData, SignatureBucketKey? key)
        {
            if (sourceData is null)
                throw new ArgumentNullException(nameof(sourceData));

            await ReadAsync().ConfigureAwait(false);

            byte[] hashValue = null!;

            var keyInts = _keys.FirstOrDefault()?.Values;

            if (Inner.IsSsh)
            {
                await CreateHash(sourceData, x => hashValue = x, _hashAlgorithm).ConfigureAwait(false);

                // SSH signature signs blob that contains original hash and some other data
                var toSign = _signBlob!.AsBucket() + NetBitConverter.GetBytes(hashValue.Length).AsBucket() + hashValue.AsBucket();

                OpenPgpHashAlgorithm? overrideAlg = null;

                if (_signaturePublicKeyType == OpenPgpPublicKeyType.Dsa)
                    overrideAlg = OpenPgpHashAlgorithm.SHA1;
                else if (_signaturePublicKeyType == OpenPgpPublicKeyType.ECDSA)
                {
                    var curveName = Encoding.ASCII.GetString((key?.Values[0] ?? keyInts![2]).ToArray());

                    overrideAlg = OpenPgpHashAlgorithm.SHA512;

                    if (curveName.EndsWith("256", StringComparison.Ordinal) || curveName.EndsWith("256r1", StringComparison.Ordinal))
                        overrideAlg = OpenPgpHashAlgorithm.SHA256;
                    else if (curveName.EndsWith("384", StringComparison.Ordinal))
                        overrideAlg = OpenPgpHashAlgorithm.SHA384;
                    else if (curveName.EndsWith("521", StringComparison.Ordinal))
                        overrideAlg = OpenPgpHashAlgorithm.SHA512;
                }
                else if (_signaturePublicKeyType == OpenPgpPublicKeyType.Rsa && Inner.IsSsh)
                    overrideAlg = OpenPgpHashAlgorithm.SHA512;

                if (_signaturePublicKeyType != OpenPgpPublicKeyType.Ed25519) // Ed25519 doesn't use a second hash
                    await CreateHash(toSign, x => hashValue = x, overrideAlg ?? _hashAlgorithm).ConfigureAwait(false);
                else
                    hashValue = toSign.ToArray();

                if (key is null && keyInts is null)
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

            var v = key?.Values ?? keyInts ?? throw new InvalidOperationException("No key to verify with");

            return VerifySignatureAsync(hashValue, v);
        }

        private bool VerifySignatureAsync(byte[] hashValue, IReadOnlyList<ReadOnlyMemory<byte>> keyValues)
        {
            if (_signatureInts == null)
                throw new InvalidOperationException("No signature value found to verify against");

            switch (_signaturePublicKeyType)
            {
                case OpenPgpPublicKeyType.Rsa:

                    using (var rsa = RSA.Create())
                    {
                        var signature = _signatureInts![0].ToArray();

                        rsa.ImportParameters(new RSAParameters()
                        {
                            Modulus = MakeUnsignedArray(keyValues[0]),
                            Exponent = MakeUnsignedArray(keyValues[1])
                        });

                        return rsa.VerifyHash(hashValue, signature, GetDotNetHashAlgorithmName(_hashAlgorithm), RSASignaturePadding.Pkcs1);
                    }
                case OpenPgpPublicKeyType.Dsa:
                    using (var dsa = DSA.Create())
                    {
                        var signature = _signatureInts![0].ToArray();

                        try
                        {
                            dsa.ImportParameters(new DSAParameters()
                            {
                                P = MakeUnsignedArray(keyValues[0], align4: true),
                                Q = MakeUnsignedArray(keyValues[1], align4: true),
                                G = MakeUnsignedArray(keyValues[2], align4: true),
                                Y = MakeUnsignedArray(keyValues[3], align4: true)
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
                        var curveName = Encoding.ASCII.GetString(keyValues[0].ToArray());

                        // Signature must be concattenation of 2 values with same number of bytes
                        var r = MakeUnsignedArray(_signatureInts[0]);
                        var s = MakeUnsignedArray(_signatureInts[1]);

                        var klen = Math.Max(r.Length, s.Length);

                        if (curveName.EndsWith("p256", StringComparison.Ordinal))
                            klen = 32;
                        else if (curveName.EndsWith("p384", StringComparison.Ordinal))
                            klen = 48;
                        else if (curveName.EndsWith("p521", StringComparison.Ordinal))
                            klen = 66;

                        var sig = new byte[2 * klen];

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
                        var signature = _signatureInts![0].ToArray();

                        return Chaos.NaCl.Ed25519.Verify(signature, hashValue, keyValues[0].ToArray());
                        //return Cryptographic.Ed25519.CheckValid(signature, hashValue, keyValues[0].ToArray());
                    }
                case OpenPgpPublicKeyType.EdDSA:
                default:
                    throw new NotImplementedException($"Public Key type {_signaturePublicKeyType} not implemented yet");
            }
        }

        static byte[] MakeUnsignedArray(ReadOnlyMemory<byte> readOnlyMemory, bool align4)
        {
            if (align4)
            {
                int b4 = (readOnlyMemory.Length & 3);
                if (b4 == 3)
                    return new byte[] { 0 }.Concat(readOnlyMemory.ToArray()).ToArray();
                else if (b4 == 0)
                    return readOnlyMemory.ToArray();
                else if (b4 == 2) // Very unlikely edge case
                    return new byte[] { 0, 0 }.Concat(readOnlyMemory.ToArray()).ToArray();
            }
            return MakeUnsignedArray(readOnlyMemory);
        }

        static byte[] MakeUnsignedArray(ReadOnlyMemory<byte> readOnlyMemory)
        {
            if (readOnlyMemory.Span[0] == 0 && (readOnlyMemory.Length & 1) == 1)
                return readOnlyMemory.Slice(1).ToArray();
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

                return (uint)((b - 192 << 8) + b2 + 192);
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

            var len = NetBitConverter.ToInt32(bb, 0);

            if (len == 0)
                return BucketBytes.Empty;

            return await bucket.ReadExactlyAsync(len).ConfigureAwait(false);
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

        internal static string FingerprintToString(ReadOnlyMemory<byte> fingerprint)
        {
            if (fingerprint.Length == 0)
                throw new ArgumentNullException(nameof(fingerprint));

            var b0 = fingerprint.Span[0];

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

        sealed class OpenPgpContainer : WrappingBucket
        {
            bool _notFirst;
            bool _isSsh;
            bool _reading;
            bool _isDer;

            public OpenPgpContainer(Bucket inner) : base(inner)
            {
            }

            public bool IsSsh => _isSsh;

            public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
            {
                while (true)
                {
                    var (bucket, _) = await ReadPacketAsync().ConfigureAwait(false);

                    if (bucket is null)
                        return BucketBytes.Eof;
                }
            }

            public async ValueTask<(Bucket? Bucket, OpenPgpTagType Type)> ReadPacketAsync()
            {
                if (_reading)
                    throw new InvalidOperationException();
                var first = false;
                var inner = Inner;
                bool sshPublicKey = false;
                if (!_notFirst)
                {
                    var didRead = false;
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
                    else if (bb.Span.StartsWith(new byte[] { 0x00, 0x00, 0x00 }))
                    {
                        if (didRead)
                            inner = bb.ToArray().AsBucket() + Inner;

                        _isSsh = true;
                        sshPublicKey = true;
                    }
                    else if (await DerBucket.BytesMayBeDerAsync(bb).ConfigureAwait(false))
                    {
                        _isDer = true;
                        if (didRead)
                            inner = bb.ToArray().AsBucket() + Inner;
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
                        return (inner.NoDispose(), sshPublicKey ? OpenPgpTagType.PublicKey : OpenPgpTagType.Signature);
                    }
                    else
                    {
                        await Inner.ReadUntilEofAsync().ConfigureAwait(false);
                        return (null, default);
                    }
                }
                else if (_isDer)
                {
                    if (first)
                    {
                        return (new DerBucket(Inner.NoDispose()), OpenPgpTagType.DerValue);
                    }
                    else
                    {
                        await Inner.ReadUntilEofAsync().ConfigureAwait(false);
                        return (null, default);
                    }
                }
                else
                {
                    var bq = await inner.ReadByteAsync().ConfigureAwait(false);

                    if (bq is null)
                        return (null, default);

                    var b = bq.Value;
                    bool oldFormat;
                    OpenPgpTagType tag;
                    uint remaining = 0;

                    if ((b & 0x80) == 0)
                        throw new BucketException("Bad packet");

                    oldFormat = 0 == (b & 0x40);
                    if (oldFormat)
                    {
                        tag = (OpenPgpTagType)((b & 0x3c) >> 2);
                        remaining = (uint)(b & 0x3);
                    }
                    else
                        tag = (OpenPgpTagType)(b & 0x2F);

                    if (!oldFormat)
                    {
                        var len = await ReadLengthAsync(inner).ConfigureAwait(false) ?? throw new BucketEofException(Inner);

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
                    return (inner.NoDispose().TakeExactly(remaining).AtEof(() => _reading = false), tag);
                }
            }
        }
    }
}
