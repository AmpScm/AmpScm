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
using System.Diagnostics;
using System.Numerics;

namespace AmpScm.Buckets.Cryptography;


/// <summary>
/// Reads an OpenPGP or OpenSSH SignaturePublicKey
/// </summary>
public sealed class SignatureBucket : CryptoDataBucket
{
    private readonly List<PublicKeySignature> _keys = new();
    private MailAddress? _mailAddress;
    private readonly List<SignatureInfo> _signatures = new();
    private readonly Bucket _outer;

    

    public SignatureBucket(Bucket source)
        : base(new CryptoChunkBucket(source))
    {
        _outer = source;
    }

    public Func<SignaturePromptContext, string>? GetPassPhrase { get; init; }

    private protected override async ValueTask<bool> HandleChunk(Bucket bucket, CryptoTag tag)
    {
        switch (tag)
        {
            case CryptoTag.SshSignaturePublicKey:
                {
                    PgpPublicKeyType publicKeyType;
                    uint sshVersion = await bucket.ReadNetworkUInt32Async().ConfigureAwait(false);

                    if (sshVersion != 1)
                        throw new BucketException();

                    var bb = await ReadSshStringAsync(bucket).ConfigureAwait(false);
                    byte[] keyFingerprint = bb.ToArray();
                    BigInteger[]? keyInts;

                    using (var ib = keyFingerprint.AsBucket())
                    {
                        bb = await ReadSshStringAsync(ib).ConfigureAwait(false);
                        string alg = bb.ToASCIIString();

                        if (alg.StartsWith("sk-", StringComparison.Ordinal))
                            alg = alg.Substring(3);

                        publicKeyType = alg switch
                        {
                            "ssh-rsa" => PgpPublicKeyType.Rsa,
                            "ssh-dss" => PgpPublicKeyType.Dsa,
                            "ssh-ed25519" or "ssh-ed25519@openssh.com" => PgpPublicKeyType.Ed25519,
                            "ecdsa-sha2-nistp256" or "ecdsa-sha2-nistp384" or "ecdsa-sha2-nistp521" => PgpPublicKeyType.ECDSA,
                            _ => throw new NotImplementedException($"Unknown public key type: {alg}"),
                        };

                        List<BigInteger> keyList = new();
                        while (!(bb = await ReadSshStringAsync(ib).ConfigureAwait(false)).IsEof)
                        {
                            keyList.Add(bb.Memory.ToBigInteger());
                        }

                        keyInts = keyList.ToArray();

                        if (publicKeyType == PgpPublicKeyType.ECDSA)
                            keyInts = GetEcdsaValues(keyInts);
                    }

                    var algId = GetCryptoAlgorithm(publicKeyType);
                    _keys.Add(new PublicKeySignature(CreateSshFingerprint(algId, keyInts), algId, keyInts, _mailAddress));

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

                    var hashAlgorithm = sigHashAlgo switch
                    {
                        "sha256" => PgpHashAlgorithm.SHA256,
                        "sha512" => PgpHashAlgorithm.SHA512,
                        _ => throw new NotImplementedException($"Unexpected hash type: {sigHashAlgo} in SSH SignaturePublicKey from {bucket.Name} Bucket"),
                    };

                    bb = await ReadSshStringAsync(bucket).ConfigureAwait(false);
                    var signature = bb.ToArray();
                    var signBlob = signPrefix.ToArray();

                    using var b = signature.AsBucket();

                    var tp = await ReadSshStringAsync(b).ConfigureAwait(false);

                    List<BigInteger> bigInts = new();
                    int i = 0;
                    while (!(bb = await ReadSshStringAsync(b).ConfigureAwait(false)).IsEof)
                    {
                        if (SplitSignatureInt(i++, publicKeyType))
                        {
                            var s = bb.AsBucket();

                            while (!(bb = await ReadSshStringAsync(s).ConfigureAwait(false)).IsEof)
                            {
                                bigInts.Add(bb.Memory.ToBigInteger());
                            }

                            continue;
                        }

                        bigInts.Add(bb.Memory.ToBigInteger());
                    }

                    //_signatureInfo.SignatureInts = bigInts.ToArray();

                    _signatures.Add(new(SignatureType: default, Signer: default, publicKeyType, hashAlgorithm, 0, SignTime: null, signBlob, bigInts.ToArray(), keyFingerprint));
                    break;
                }
            case CryptoTag.SshPublicKey:
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

                    if (PublicKeySignature.TryParseSshLine(k, out var kk))
                    {
                        _keys.Add(kk);
                    }
                    break;
                }
            case CryptoTag.SignaturePublicKey:
                _signatures.Add(await ParseSignatureAsync(bucket).ConfigureAwait(false));
                break;
            case CryptoTag.PublicKey:
            case CryptoTag.PublicSubkey:
            case CryptoTag.SecretKey:
            case CryptoTag.SecretSubkey:
                {
                    DateTime keyTime;
                    BigInteger[]? keyInts;
                    byte[]? keyFingerprint = null;
                    PgpPublicKeyType keyPublicKeyType;
                    bool hasSecretKey = tag is CryptoTag.SecretKey or CryptoTag.SecretSubkey;

                    var csum = bucket.NoDispose().Buffer();
                    uint len = (uint)await csum.ReadRemainingBytesAsync().ConfigureAwait(false);
                    byte version = await csum.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(bucket);

                    if (version == 4 || version == 5)
                    {
                        var bb = await csum.ReadExactlyAsync(5).ConfigureAwait(false);

                        if (bb.Length != 5)
                            throw new BucketEofException(bucket);

                        keyTime = DateTimeOffset.FromUnixTimeSeconds(NetBitConverter.ToUInt32(bb, 0)).DateTime;
                        keyPublicKeyType = (PgpPublicKeyType)bb[4];

                        if (version == 5)
                            await csum.ReadNetworkInt32Async().ConfigureAwait(false); // Length of what follows
                    }
                    else if (version == 3)
                    {
                        throw new NotImplementedException("Version 3 SignaturePublicKey not implemented yet");
                    }
                    else
                        throw new NotImplementedException("Only OpenPGP public key versions 3, 4 and 5 are supported");

                    List<BigInteger> bigInts = new();

                    int nrOfInts = keyPublicKeyType switch
                    {
                        PgpPublicKeyType.Rsa or
                        PgpPublicKeyType.RsaEncryptOnly or
                        PgpPublicKeyType.RsaSignOnly => 2,
                        PgpPublicKeyType.Elgamal => 3,
                        PgpPublicKeyType.Dsa => 4,
                        PgpPublicKeyType.ECDH => 3,
                        PgpPublicKeyType.ECDSA => 2,
                        PgpPublicKeyType.EdDSA => 2,
                        _ => throw new NotImplementedException($"Unexpected public key type {keyPublicKeyType}")
                    };
                    if (keyPublicKeyType is PgpPublicKeyType.EdDSA or PgpPublicKeyType.ECDSA or PgpPublicKeyType.ECDH)
                    {
                        byte b = await csum.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(csum);

                        if (b == 0 || b == 0xFF)
                            throw new NotImplementedException("Reserved value");

                        var bb = await csum.ReadExactlyAsync(b).ConfigureAwait(false);
                        if (bb.Length != b)
                            throw new BucketEofException(bucket);

                        bigInts.Add(bb.Memory.ToBigInteger()); // [0] = OID
                    }

                    if (keyPublicKeyType is PgpPublicKeyType.ECDH)
                    {
                        var bi = await ReadPgpMultiPrecisionInteger(csum).ConfigureAwait(false);

                        bigInts.Add(bi.Value); // [1] = Q

                        byte b = await csum.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(csum);

                        if (b == 0 || b == 0xFF)
                            throw new NotImplementedException("Reserved value");

                        var bb = await csum.ReadExactlyAsync(b).ConfigureAwait(false);
                        if (bb.Length != b)
                            throw new BucketEofException(bucket);

                        bigInts.Add(bb.Memory.ToBigInteger()); // [2] = KDF

                        // [3] = D
                    }


                    while (bigInts.Count < nrOfInts && await ReadPgpMultiPrecisionInteger(csum).ConfigureAwait(false) is { } bi)
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
                        else if (sku is 254 or 255 or 253)
                        {
                            var cipherAlgorithm = (PgpSymmetricAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);

                            var s2k = await ReadPgpS2kSpecifierAsync(bucket, cipherAlgorithm).ConfigureAwait(false);

                            if (GetPassPhrase?.Invoke(SignaturePromptContext.Empty) is not { } password)
                            {
                                hasSecretKey = false;
                                await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                            }
                            else
                            {
                                byte[] key = DeriveS2kKey(s2k, password);

                                if (sku is 254 or 255) // Normal generation
                                {
                                    byte[] iv = (await bucket.ReadExactlyAsync(key.Length).ConfigureAwait(false)).ToArray();

                                    keySrc = CreateDecryptBucket(bucket, s2k.CipherAlgorithm, key, iv);
                                }
                                else
                                {
                                    // OCB

                                    throw new NotImplementedException("OCB Encrypted key");

                                }
                            }

                        }
                        else
                        {
                            //var akg -sju;
                            throw new InvalidOperationException("Unknown S2k sku specifier"); // More specifics based on the algorithm
                        }

                        if (hasSecretKey)
                        {
                            switch (keyPublicKeyType)
                            {
                                case PgpPublicKeyType.Rsa:
                                    nrOfInts += 4;
                                    break;
                                case PgpPublicKeyType.Dsa:
                                    nrOfInts += 1;
                                    break;
                                case PgpPublicKeyType.Elgamal:
                                    nrOfInts += 1;
                                    break;
                                default:
                                    if (version >= 4)
                                        nrOfInts += 1; // Encoded in one int
                                    else
                                        throw new NotImplementedException($"Unexpected private key key type {keyPublicKeyType}");
                                    break;
                            }

                            while (bigInts.Count < nrOfInts && await ReadPgpMultiPrecisionInteger(keySrc).ConfigureAwait(false) is { } bi)
                            {
                                bigInts.Add(bi);
                            }


                            if (nrOfInts != bigInts.Count)
                                throw new BucketException($"Didn't get the {nrOfInts} big integers required for a {keyPublicKeyType} key");


                            if (sku is 0 or 255)
                            {
                                var cs = await bucket.ReadNetworkUInt16Async().ConfigureAwait(false);
                            }
                            else if (sku is 254)
                            {
                                var sha1 = await bucket.ReadExactlyAsync(20).ConfigureAwait(false);
                            }
                        }
                    }

                    long rem = await bucket.ReadUntilEofAsync().ConfigureAwait(false);

                    if (rem > 0)
                        throw new BucketException($"Unexpected data after {keyPublicKeyType} key");

                    keyInts = bigInts.ToArray();
                    if (keyPublicKeyType == PgpPublicKeyType.EdDSA && keyInts[0].ToCryptoValue().SequenceEqual(new byte[] { 0x2B, 0x06, 0x01, 0x04, 0x01, 0xDA, 0x47, 0x0F, 0x01 }))
                    {
                        // This algorithm is not implemented by .Net, but for this specific curve we have a workaround
                        keyPublicKeyType = PgpPublicKeyType.Ed25519;
                        // Convert `0x40 | value` form to `value`
                        keyInts = new[] { keyInts[1].ToCryptoValue().Skip(1).ToBigInteger() };
                    }
                    else if (keyPublicKeyType == PgpPublicKeyType.ECDH && keyInts[0].ToCryptoValue().SequenceEqual(new byte[] { 0x2B, 0x06, 0x01, 0x04, 0x01, 0x97, 0x55, 0x01, 0x05, 0x01 }))
                    {
                        keyPublicKeyType = PgpPublicKeyType.Curve25519;
                        keyInts = keyInts.Skip(1).ToArray();
                    }

                    _keys.Add(new PublicKeySignature(keyFingerprint!, GetCryptoAlgorithm(keyPublicKeyType), keyInts, _mailAddress, hasSecretKey));
                }
                break;
            case CryptoTag.DerValue:
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
                        List<BigInteger> vals = new();

                        while (await der.ReadValueAsync().ConfigureAwait(false) is { } rd && rd.Bucket is { })
                        {
                            var (b, bt) = rd;

                            bb = await b.ReadExactlyAsync(8192).ConfigureAwait(false);
                            vals.Add(bb.Memory.ToBigInteger());
                        }

                        var keyInts = vals.ToArray();

                        _keys.Add(new PublicKeySignature(CreateSshFingerprint(CryptoAlgorithm.Rsa, keyInts), CryptoAlgorithm.Rsa, keyInts));
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

                        CryptoAlgorithm cryptoAlg;

                        if (bb.Span.SequenceEqual(new byte[] { 0x2a, 0x86, 0x48, 0xce, 0x38, 0x04, 0x01 }))
                            cryptoAlg = CryptoAlgorithm.Dsa;
                        else if (bb.Span.SequenceEqual(new byte[] { 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x02, 0x01 }))
                            cryptoAlg = CryptoAlgorithm.Ecdsa;
                        else
                        {
                            await ob!.ReadUntilEofAsync().ConfigureAwait(false);
                            await der2.ReadUntilEofAsync().ConfigureAwait(false);
                            await der.ReadUntilEofAsync().ConfigureAwait(false);
                            break;
                        }

                        List<BigInteger> vals = new();

                        await SequenceToList(vals, der2).ConfigureAwait(false);
                        await SequenceToList(vals, der).ConfigureAwait(false);

                        BigInteger[] keyInts = vals.ToArray();

                        _keys.Add(new PublicKeySignature(CreateSshFingerprint(cryptoAlg, keyInts), cryptoAlg, keyInts));
                    }
                    else
                        await der.ReadUntilEofAsync().ConfigureAwait(false);
                }
                break;
            case CryptoTag.UserID:
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

                if (_keys.Count > 0)
                    _keys[0] = _keys[0].WithSubKeys(_keys[0].SubKeys.Cast<PublicKeySignature>(), _mailAddress);
                break;
            default:
                break;
        }


        {
            var rem = await bucket.ReadUntilEofAsync().ConfigureAwait(false);

            Debug.Assert(rem == 0, "Read whole packets");
        }

        return true;
    }

    public async ValueTask<PublicKeySignature> ReadKeyAsync()
    {
        await ReadAsync().ConfigureAwait(false);
        if (_keys.Count == 1)
            return _keys[0];

        return _keys[0].WithSubKeys(_keys.Skip(1), _mailAddress);
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadFingerprintAsync()
    {
        await ReadAsync().ConfigureAwait(false);

        var b = _signatures.FirstOrDefault()?.SignKeyFingerprint;

        if (b[0] >= 3 && b[0] <= 5)
            return b.AsMemory(1);
        else
            return b;
    }

    public async ValueTask<bool> VerifyAsync(Bucket sourceData, PublicKeySignature? key, bool unknownSignerOk = false)
    {
        if (sourceData is null)
            throw new ArgumentNullException(nameof(sourceData));

        await ReadAsync().ConfigureAwait(false);

        if (_signatures.Count == 0)
            throw new InvalidOperationException();

        if (Source.IsSsh)
            return await VerifyAsyncSsh(sourceData, key).ConfigureAwait(false);


        int needResults = 0;
        foreach (var sig in _signatures)
        {
            var info = _signatures.First();

            var keyInts = _keys.FirstOrDefault()?.GetValues(false);

            var hashAlgorithm = CreatePgpHashAlgorithm(info.HashAlgorithm);

            sourceData = sourceData.Hash(hashAlgorithm, (completer) =>
            {
                var hashValue = completer(info.SignBlob);

                if (NetBitConverter.ToUInt16(hashValue, 0) != info.HashStart)
                    return; // Quick check failed

                var ak = key ?? KeyChain?.FindKey(info.SignKeyFingerprint);

                var v = ak?.GetValues() ?? keyInts;

                if (key is { })
                {
                    if (QVerifySignature(info, hashValue, v ?? throw new InvalidOperationException()))
                    {
                        needResults -= 1;
                    }
                    // Bad signature, error!
                }
                else if (unknownSignerOk)
                {
                    // No key. For now we say this is ok

                    needResults -= 1;
                }
            });

            needResults++;
        }

        await sourceData.ReadUntilEofAndCloseAsync().ConfigureAwait(false);

        return needResults == 0;
    }

    private async ValueTask<bool> VerifyAsyncSsh(Bucket sourceData, PublicKeySignature? key)
    {
        var _signatureInfo = _signatures.First();
        byte[] hashValue;

        var keyInts = _keys.FirstOrDefault()?.GetValues(false);
        hashValue = await CalculateHash(sourceData, _signatureInfo.HashAlgorithm).ConfigureAwait(false);

        // SSH SignaturePublicKey signs blob that contains original hash and some other data
        var toSign = _signatureInfo.SignBlob!.AsBucket() + NetBitConverter.GetBytes(hashValue.Length).AsBucket() + hashValue.AsBucket();

        PgpHashAlgorithm? overrideAlg = null;

        if (_signatureInfo.PublicKeyType == PgpPublicKeyType.Dsa)
            overrideAlg = PgpHashAlgorithm.SHA1;
        else if (_signatureInfo.PublicKeyType == PgpPublicKeyType.ECDSA)
        {
            string curveName = CryptoExtensions.GetCurveName(key?.GetValues(false)[0] ?? keyInts![2]);

            overrideAlg = PgpHashAlgorithm.SHA512;

            if (curveName.EndsWith("256", StringComparison.Ordinal) || curveName.EndsWith("256r1", StringComparison.Ordinal))
                overrideAlg = PgpHashAlgorithm.SHA256;
            else if (curveName.EndsWith("384", StringComparison.Ordinal))
                overrideAlg = PgpHashAlgorithm.SHA384;
            else if (curveName.EndsWith("521", StringComparison.Ordinal))
                overrideAlg = PgpHashAlgorithm.SHA512;
        }
        else if (_signatureInfo.PublicKeyType == PgpPublicKeyType.Rsa && Source.IsSsh)
            overrideAlg = PgpHashAlgorithm.SHA512;

        if (_signatureInfo.PublicKeyType != PgpPublicKeyType.Ed25519) // Ed25519 doesn't use a second hash
            hashValue = await CalculateHash(toSign, overrideAlg ?? _signatureInfo.HashAlgorithm).ConfigureAwait(false);
        else
            hashValue = toSign.ToArray();

        if (key is null && keyInts is null)
            return false; // Can't verify SSH SignaturePublicKey without key (yet)

        var v = key?.GetValues() ?? keyInts ?? throw new InvalidOperationException("No key to verify with");

        return QVerifySignature(_signatureInfo, hashValue, v);
    }
}


