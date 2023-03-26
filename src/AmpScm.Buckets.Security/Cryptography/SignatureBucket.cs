using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;
using System.Globalization;
using AmpScm.Buckets;
using System.Net.Mail;

namespace AmpScm.Buckets.Cryptography
{

    /// <summary>
    /// Reads an OpenPGP or OpenSSH signature
    /// </summary>
    public sealed class SignatureBucket : CryptoDataBucket
    {
        private OpenPgpSignatureType _signatureType;
        private byte[]? _signer;
        private OpenPgpPublicKeyType _signaturePublicKeyType;
        private OpenPgpHashAlgorithm _hashAlgorithm;
        private ushort _hashStart;
        private DateTime? _signTime;
        private byte[]? _signature;
        private byte[]? _signBlob;
        private ReadOnlyMemory<byte>[]? _signatureInts;
        private readonly List<Signature> _keys = new();
        private byte[]? _signKeyFingerprint;
        private MailAddress? _mailAddress;
        private readonly Bucket _outer;

        private new OpenPgpContainer Source => (OpenPgpContainer)base.Source;

        public SignatureBucket(Bucket source)
            : base(new OpenPgpContainer(source))
        {
            _outer = source;
        }

        public Func<SignaturePromptContext, string>? GetPassPhrase { get; init; }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            await ReadAsync().ConfigureAwait(false);

            var bb = await Source.ReadAsync(requested).ConfigureAwait(false);

            if (!bb.IsEof)
                throw new BucketException($"Unexpected trailing data in {Source.Name} Bucket");

            return bb;
        }

        public async ValueTask ReadAsync()
        {
            while (true)
            {
                var (bucket, tag) = await Source.ReadPacketAsync().ConfigureAwait(false);

                if (bucket is null)
                    return;

                using (bucket)
                {
                    switch (tag)
                    {
                        case OpenPgpTagType.Signature when Source.IsSsh && _signatureInts is null:
                            {
                                uint sshVersion = await bucket.ReadNetworkUInt32Async().ConfigureAwait(false);

                                if (sshVersion != 1)
                                    throw new BucketException();

                                var bb = await ReadSshStringAsync(bucket).ConfigureAwait(false);
                                byte[] keyFingerprint = bb.ToArray();
                                ReadOnlyMemory<byte>[]? keyInts;

                                using (var ib = keyFingerprint.AsBucket())
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
                                _keys.Add(new Signature(keyFingerprint!, GetKeyAlgo(_signaturePublicKeyType), keyInts, _mailAddress));

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

                                string sigHashAlgo = bb.ToASCIIString();

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
                        case OpenPgpTagType.PublicKey when Source.IsSsh:
                            {
                                BucketBytes bb;
                                ByteCollector bc = new();
                                string? sshKeyType = null;

                                while (!(bb = await ReadSshStringAsync(bucket).ConfigureAwait(false)).IsEof)
                                {
                                    sshKeyType ??= bb.ToASCIIString();

                                    bc.Append(NetBitConverter.GetBytes(bb.Length));
                                    bc.Append(bb);
                                }

                                string k = $"{sshKeyType} {Convert.ToBase64String(bc.ToArray())} bb";

                                if (Signature.TryParseSshLine(k, out var kk))
                                {
                                    _keys.Add(kk);
                                }
                                break;
                            }
                        case OpenPgpTagType.Signature when _signatureInts is null:
                            (_signatureType, _signer, _signaturePublicKeyType, _hashAlgorithm, _hashStart, _signTime, _signBlob, _signatureInts, _signKeyFingerprint)
                            = await ParseSignatureAsync(bucket).ConfigureAwait(false);
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
                                uint len = (uint)await csum.ReadRemainingBytesAsync().ConfigureAwait(false);
                                byte version = await csum.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(bucket);

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
                                    byte b = await csum.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(csum);

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

                                    byte b = await csum.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(csum);

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

                                if (nrOfInts != bigInts.Count)
                                    throw new BucketException($"Didn't get the {nrOfInts} big integers required for a {keyPublicKeyType} key");

                                long take = csum.Position ?? len;
                                csum.Reset();

                                if (version == 4)
                                {
                                    await (new byte[] { 0x99 }.AsBucket() + NetBitConverter.GetBytes((ushort)take).AsBucket() + csum.Take(take))
                                        .SHA1(x => keyFingerprint = x)
                                        .ReadUntilEofAndCloseAsync().ConfigureAwait(false);
                                }
                                else if (version == 5)
                                {
                                    await (new byte[] { 0x9A }.AsBucket() + NetBitConverter.GetBytes((int)take).AsBucket() + csum.Take(take))
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

                                if (hasSecretKey)
                                {
                                    byte sku = await bucket.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(bucket);
                                    Bucket keySrc = bucket;

                                    if (sku == 0)
                                    {
                                        // Not encrypted
                                    }
                                    else if (sku is 254 or 255)
                                    {
                                        var cipherAlgorithm = (OpenPgpSymmetricAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);

                                        var s2k = await ReadPgpS2kSpecifierAsync(bucket, cipherAlgorithm).ConfigureAwait(false);

                                        if (GetPassPhrase?.Invoke(SignaturePromptContext.Empty) is not { } password)
                                        {
                                            hasSecretKey = false;
                                            await bucket.ReadUntilEofAsync().ConfigureAwait(false);

                                        }
                                        else
                                        {
                                            byte[] key = DeriveS2kKey(s2k, password);

                                            byte[] iv = (await bucket.ReadExactlyAsync(key.Length).ConfigureAwait(false)).ToArray();

                                            keySrc = CreateDecryptBucket(bucket, s2k.CipherAlgorithm, key, iv);
                                        }

                                    }
                                    else
                                    {
                                        //var akg -sju;
                                        throw new InvalidOperationException(); // More specifics based on the algorithm
                                    }

                                    if (hasSecretKey)
                                    {
                                        switch (keyPublicKeyType)
                                        {
                                            case OpenPgpPublicKeyType.Rsa:
                                                nrOfInts += 4;
                                                break;
                                            case OpenPgpPublicKeyType.Dsa:
                                                nrOfInts += 1;
                                                break;
                                            case OpenPgpPublicKeyType.Elgamal:
                                                nrOfInts += 1;
                                                break;
                                            default:
                                                if (version >= 4)
                                                    nrOfInts += 1; // Encoded in one int
                                                else
                                                    throw new NotImplementedException($"Unexpected private key key type {keyPublicKeyType}");
                                                break;
                                        }

                                        while (bigInts.Count < nrOfInts && await ReadPgpMultiPrecisionInteger(keySrc).ConfigureAwait(false) is ReadOnlyMemory<byte> bi)
                                        {
                                            bigInts.Add(bi);
                                        }


                                        if (nrOfInts != bigInts.Count)
                                            throw new BucketException($"Didn't get the {nrOfInts} big integers required for a {keyPublicKeyType} key");


                                        if (sku is 0 or 255)
                                        {
                                            await bucket.ReadNetworkUInt16Async().ConfigureAwait(false);
                                        }
                                    }
                                }

                                long rem = await bucket.ReadUntilEofAsync().ConfigureAwait(false);

                                if (rem > 0)
                                    throw new BucketException($"Unexpected data after {keyPublicKeyType} key");

                                keyInts = bigInts.ToArray();
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

                                _keys.Add(new Signature(keyFingerprint!, GetKeyAlgo(keyPublicKeyType), keyInts, _mailAddress, hasSecretKey));
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

                                    _keys.Add(new Signature(CreateSshFingerprint(SignatureAlgorithm.Rsa, keyInts), SignatureAlgorithm.Rsa, keyInts));
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

                                    SignatureAlgorithm sba;

                                    if (bb.Span.SequenceEqual(new byte[] { 0x2a, 0x86, 0x48, 0xce, 0x38, 0x04, 0x01 }))
                                        sba = SignatureAlgorithm.Dsa;
                                    else if (bb.Span.SequenceEqual(new byte[] { 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x02, 0x01 }))
                                        sba = SignatureAlgorithm.Ecdsa;
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

                                    if (sba == SignatureAlgorithm.Ecdsa && vals.Count >= 2)
                                    {
                                        vals[1] = vals[1].Slice(1).ToArray();
                                        keyInts = GetEcdsaValues(vals, true);
                                    }
                                    else
                                        keyInts = vals.ToArray();

                                    _keys.Add(new Signature(CreateSshFingerprint(sba, keyInts), sba, keyInts));
                                }
                                else
                                    await der.ReadUntilEofAsync().ConfigureAwait(false);
                            }
                            break;
                        case OpenPgpTagType.UserID:
                            {
                                var bb = await bucket.ReadExactlyAsync(MaxRead).ConfigureAwait(false);

                                string name = bb.ToUTF8String();

#if NETCOREAPP
                                if (MailAddress.TryCreate(name, out var result))
                                    _mailAddress = result;
                                else
#endif
                                {
                                    int n = name.LastIndexOf('<');

                                    if (n > 0)
                                        _mailAddress = new MailAddress(name.Substring(n + 1).Trim('>'), name.Substring(0, n).Trim());
                                    else
                                        _mailAddress = new MailAddress("no@one", name);
                                }
                            }

                            if (_keys.Count > 1)
                                _keys[0] = _keys[0].WithSubKeys(_keys[0].SubKeys, _mailAddress);
                            break;
                        case OpenPgpTagType.Signature:
                            await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                            break;
                        default:
#if DEBUG
                            throw new NotImplementedException();
#endif
                            await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                            break;
                    }
                }
            }
        }

        internal static async ValueTask<(OpenPgpSignatureType signatureType, byte[]? signer, OpenPgpPublicKeyType signaturePublicKeyType, OpenPgpHashAlgorithm hashAlgorithm, ushort hashStart, DateTime? signTime, byte[]? signBlob, ReadOnlyMemory<byte>[]? signatureInts, byte[]? signKeyFingerprint)>
        ParseSignatureAsync(Bucket bucket)
        {
            OpenPgpSignatureType _signatureType;
            byte[]? _signer = null;
            OpenPgpPublicKeyType _signaturePublicKeyType;
            OpenPgpHashAlgorithm _hashAlgorithm;
            ushort _hashStart;
            DateTime? _signTime = null;
            byte[]? _signBlob;
            ReadOnlyMemory<byte>[]? _signatureInts;
            byte[]? _signKeyFingerprint = null;

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

                        byte b = await subRead.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(subRead);
                        len--;

                        switch ((OpenPgpSubPacketType)b)
                        {
                            case OpenPgpSubPacketType.SignatureCreationTime:
                                if (len != 4)
                                    throw new InvalidOperationException();

                                int time = await subRead.ReadNetworkInt32Async().ConfigureAwait(false);

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
                            case OpenPgpSubPacketType.PreferredAeadAlgorithms:
                                if (len != await subRead.ReadSkipAsync(len.Value).ConfigureAwait(false))
                                    throw new BucketEofException(subRead);
                                break;

                            case OpenPgpSubPacketType.SignersUserID:
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


            return (_signatureType, _signer, _signaturePublicKeyType, _hashAlgorithm, _hashStart, _signTime, _signBlob, _signatureInts, _signKeyFingerprint);
        }

        private static ReadOnlyMemory<byte> CreateSshFingerprint(SignatureAlgorithm sba, ReadOnlyMemory<byte>[] keyInts)
        {
            ByteCollector bb = new(4096);

            var ints = keyInts.ToList();

            string alg;
            switch (sba)
            {
                case SignatureAlgorithm.Rsa:
                    alg = "ssh-rsa";
                    (ints[1], ints[0]) = (ints[0], ints[1]);
                    break;
                case SignatureAlgorithm.Dsa:
                    alg = "ssh-dss";
                    break;
                case SignatureAlgorithm.Ed25519:
                    alg = "ssh-ed25519";
                    break;
                case SignatureAlgorithm.Ecdsa:
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

        public async ValueTask<Signature> ReadKeyAsync()
        {
            await ReadAsync().ConfigureAwait(false);
            if (_keys.Count == 1)
                return _keys[0];

            return _keys[0].WithSubKeys(_keys.Skip(1), _mailAddress);
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReadFingerprintAsync()
        {
            await ReadAsync().ConfigureAwait(false);

            return _signKeyFingerprint;
        }

        public async ValueTask<bool> VerifyAsync(Bucket sourceData, Signature? key)
        {
            if (sourceData is null)
                throw new ArgumentNullException(nameof(sourceData));

            await ReadAsync().ConfigureAwait(false);

            byte[] hashValue = null!;

            var keyInts = _keys.FirstOrDefault()?.Values;

            if (Source.IsSsh)
            {
                await CreateHash(sourceData, x => hashValue = x, _hashAlgorithm).ConfigureAwait(false);

                // SSH signature signs blob that contains original hash and some other data
                var toSign = _signBlob!.AsBucket() + NetBitConverter.GetBytes(hashValue.Length).AsBucket() + hashValue.AsBucket();

                OpenPgpHashAlgorithm? overrideAlg = null;

                if (_signaturePublicKeyType == OpenPgpPublicKeyType.Dsa)
                    overrideAlg = OpenPgpHashAlgorithm.SHA1;
                else if (_signaturePublicKeyType == OpenPgpPublicKeyType.ECDSA)
                {
                    string curveName = Encoding.ASCII.GetString((key?.Values[0] ?? keyInts![2]).ToArray());

                    overrideAlg = OpenPgpHashAlgorithm.SHA512;

                    if (curveName.EndsWith("256", StringComparison.Ordinal) || curveName.EndsWith("256r1", StringComparison.Ordinal))
                        overrideAlg = OpenPgpHashAlgorithm.SHA256;
                    else if (curveName.EndsWith("384", StringComparison.Ordinal))
                        overrideAlg = OpenPgpHashAlgorithm.SHA384;
                    else if (curveName.EndsWith("521", StringComparison.Ordinal))
                        overrideAlg = OpenPgpHashAlgorithm.SHA512;
                }
                else if (_signaturePublicKeyType == OpenPgpPublicKeyType.Rsa && Source.IsSsh)
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
                        byte[] signature = _signatureInts![0].ToArray();

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
                        byte[] signature = _signatureInts![0].ToArray();

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
                    }
                case OpenPgpPublicKeyType.EdDSA:
                default:
                    throw new NotImplementedException($"Public Key type {_signaturePublicKeyType} not implemented yet");
            }
        }


    }
}


