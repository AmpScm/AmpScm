using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using AmpScm.Buckets;
using AmpScm.Buckets.Cryptography.Algorithms;
using AmpScm.Buckets.Specialized;

// https://www.rfc-editor.org/rfc/rfc4880
// https://datatracker.ietf.org/doc/draft-koch-openpgp-2015-rfc4880bis/

namespace AmpScm.Buckets.Cryptography;

public sealed class DecryptBucket : CryptoDataBucket
{
    private PgpSymmetricAlgorithm _sessionAlgorithm;
    private ReadOnlyMemory<byte> _sessionKey; // The symetric key
    private string? _fileName;
    private DateTime? _fileDate;
#pragma warning disable CA2213 // Disposable fields should be disposed
    private Bucket? _literalSha;
#pragma warning restore CA2213 // Disposable fields should be disposed
    private byte[]? _shaResult;
    private readonly Stack<PgpSignature> _sigs = new();
    private int _verifies;

    public DecryptBucket(Bucket source)
        : base(new CryptoChunkBucket(source))
    {
    }

    public new bool VerifySignature { get; init; }

#pragma warning disable MA0051 // Method is too long
    private protected override async ValueTask<bool> HandleChunk(Bucket bucket, CryptoTag tag)
#pragma warning restore MA0051 // Method is too long
    {
        switch (tag)
        {
            case CryptoTag.CompressedData:
                {
                    byte? b = await bucket.ReadByteAsync().ConfigureAwait(false);

                    Bucket rd;
                    switch ((PgpCompressionType)b)
                    {
                        case PgpCompressionType.None:
                            rd = bucket;
                            break;
                        case PgpCompressionType.Zip:
                            rd = new ZLibBucket(bucket, BucketCompressionAlgorithm.Deflate);
                            break;
                        case PgpCompressionType.Zlib:
                            rd = new ZLibBucket(bucket, BucketCompressionAlgorithm.ZLib);
                            break;
                        default:
                            throw new NotSupportedException($"Compression algorithm {(PgpCompressionType)b} not implemented");
                    }

                    PushChunkReader(new CryptoChunkBucket(rd.NoDispose()));
                    return false;
                }

            case CryptoTag.PublicKeySession:
                {
                    byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                    // Read the public key, for who the file is encrypted
                    var bb = (await bucket.ReadAtLeastAsync(8, throwOnEndOfStream: false).ConfigureAwait(false));

                    if (!_sessionKey.IsEmpty)
                    {
                        await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                        break; // Skip packet, we already have a session
                    }

                    var fingerprint = bb.ToArray();
                    var pca = (PgpPublicKeyType)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);

                    var key = KeyChain?.FindKey(fingerprint, requirePrivateKey: true) as PublicKeySignature;

                    if (!(key?.HasPrivateKey ?? false) || key?.MatchFingerprint(fingerprint, requirePrivateKey: true) is not { } matchedKey)
                    {
                        await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                        break; // Ignore rest of packet
                    }

                    var keyValues = matchedKey.GetValues(includePrivate: true);

                    switch (matchedKey.Algorithm)
                    {
                        case CryptoAlgorithm.Rsa:
                            using (var rsa = RSA.Create())
                            {
                                rsa.ImportParametersFromCryptoValues(keyValues);

                                var bi = await ReadPgpMultiPrecisionInteger(bucket).ConfigureAwait(false);

                                try
                                {
                                    byte[] data = rsa.Decrypt(bi.Value.ToCryptoValue(), RSAEncryptionPadding.Pkcs1);

                                    SetupSessionFromDecrypted(data);
                                }
                                catch (CryptographicException)
                                { }
                            }
                            break;

                        case CryptoAlgorithm.Ecdh:
                            using (var ecdh = ECDiffieHellman.Create())
                            {
                                ecdh.ImportParametersFromCryptoValues(keyValues);

                                var scalar = await ReadPgpMultiPrecisionInteger(bucket).ConfigureAwait(false) ?? throw new BucketEofException(bucket);

                                using var pk = ecdh.CreatePublicKey(scalar);


                                var oid = keyValues[0].ToCryptoValue();
                                var (kdfHash, kdfCipher, ecdhParams) = CreateEcdhParams(matchedKey, keyValues[2], oid);

                                byte[] kek = ecdh.DeriveKeyFromHash(pk, kdfHash.GetHashAlgorithmName(),
                                                                        secretPrepend: new byte[] { 0, 0, 0, 1 }, secretAppend: ecdhParams)
                                            .Take(kdfCipher.GetKeyBytes()).ToArray();


                                byte len = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                                if (len > 0)
                                {
                                    var srcData = await bucket.ReadExactlyAsync(len).ConfigureAwait(false);

                                    if (new Rfc3394Algorithm(kek).TryUnwrapKey(srcData.ToArray(), out var data))
                                    {
                                        SetupSessionFromDecrypted(data);
                                    }
                                }
                            }
                            break;

                        case CryptoAlgorithm.Elgamal:
                            using (var elgamal = Elgamal.Create())
                            {
                                elgamal.ImportParametersFromCryptoValues(keyValues);


                                var b1 = await ReadPgpMultiPrecisionInteger(bucket).ConfigureAwait(false) ?? throw new BucketEofException(bucket); // g**k mod p.
                                var b2 = await ReadPgpMultiPrecisionInteger(bucket).ConfigureAwait(false) ?? throw new BucketEofException(bucket); // y**k mod p


                                var data = elgamal.Decrypt(b1, b2, ElgamalEncryptionPadding.Pkcs1);

                                SetupSessionFromDecrypted(data);
                            }
                            break;

                        case CryptoAlgorithm.Curve25519:
                            using (var curve25519 = Curve25519.Create())
                            {
                                curve25519.ImportParametersFromCryptoInts(keyValues);

                                var scalar = await ReadPgpMultiPrecisionInteger(bucket).ConfigureAwait(false) ?? throw new BucketEofException(bucket);

                                var pk = curve25519.CreatePublicKey(scalar);

                                var oid = keyValues[0].ToCryptoValue();
                                var (kdfHash, kdfCipher, ecdhParams) = CreateEcdhParams(matchedKey, keyValues[2], oid);

                                using var ha = CreatePgpHashAlgorithm(kdfHash);
                                byte[] kek = curve25519.DeriveKeyFromHash(pk, ha, secretPrepend: new byte[] { 0, 0, 0, 1 }, secretAppend: ecdhParams)
                                            .Take(kdfCipher.GetKeyBytes()).ToArray();

                                byte len = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                                if (len > 0)
                                {
                                    var srcData = await bucket.ReadExactlyAsync(len).ConfigureAwait(false);

                                    if (new Rfc3394Algorithm(kek).TryUnwrapKey(srcData.ToArray(), out var data))
                                    {
                                        SetupSessionFromDecrypted(data);
                                    }
                                }
                            }
                            break;

                        default:
                            throw new NotSupportedException($"Algorithm {matchedKey.Algorithm} not implemented yet");
                    }
                }
                break;
            case CryptoTag.OnePassSignature:
                {
                    byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                    var signatureType = (PgpSignatureType)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
                    var hashAlgorithm = (PgpHashAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
                    var pkt = (PgpPublicKeyType)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);

                    byte[] signer = (await bucket.ReadAtLeastAsync(8, throwOnEndOfStream: false).ConfigureAwait(false)).ToArray();

                    byte flag = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                    GC.KeepAlive(flag);

                    _sigs.Push(new(version, signatureType, hashAlgorithm, pkt, signer));
                }
                break;
            case CryptoTag.OCBEncryptedData:
                {
                    byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                    var cipherAlgorithm = (PgpSymmetricAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
                    byte ocbEncryptionAlgorithm = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                    byte chunkVal = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                    long chunk_size = 1L << (chunkVal + 6); // Usually 4MB

                    if (_sessionKey.IsEmpty)
                        throw new BucketException("Can't decrypt without valid session key");
                    else if (cipherAlgorithm != _sessionAlgorithm)
                        throw new BucketException("Session key is for different algorithm");

                    // Starting vector (OCB specific)
                    byte[] startingVector = (await bucket.ReadAtLeastAsync(OcbDecodeBucket.MaxNonceLength, throwOnEndOfStream: false).ConfigureAwait(false)).ToArray();
                    var sessionKey = _sessionKey.ToArray();

                    // Encrypted data

                    bucket = bucket.Leave(16, async (bb, length) =>
                    {
                        long n = (length + chunk_size - 1) / chunk_size; // Calculate number of chunks before the final block
                        length -= n * 16; // Remove the tags of these chunks from the total length

                        byte[] associatedData = new byte[] { 0xC0 | (int)CryptoTag.OCBEncryptedData, version, (byte)cipherAlgorithm, ocbEncryptionAlgorithm, chunkVal }
                                .Concat(NetBitConverter.GetBytes(n)).Concat(NetBitConverter.GetBytes(length)).ToArray();

                        byte[] sv = startingVector.ToArray();
                        OcbDecodeBucket.SpanXor(sv.AsSpan(sv.Length - 4), NetBitConverter.GetBytes((int)n));

                        await using var ocd = new OcbDecodeBucket(bb.Memory.AsBucket(), sessionKey, 128, sv, associatedData, verifyResult: x =>
                        {
                            if (!x)
                                throw new BucketDecryptionException($"Verification of final chunk in {bucket} bucket failed");
                        });

                        await ocd.ReadUntilEofAsync().ConfigureAwait(false);
                    });

                    bucket = new OcbChunkReader(bucket, (int)chunk_size, 16,
                        (n, data) =>
                        {
                            byte[] associatedData = new byte[] { 0xC0 | (int)CryptoTag.OCBEncryptedData, version, (byte)cipherAlgorithm, ocbEncryptionAlgorithm, chunkVal }
                                .Concat(NetBitConverter.GetBytes((long)n)).ToArray();

                            byte[] sv = startingVector.ToArray();
                            OcbDecodeBucket.SpanXor(sv.AsSpan(sv.Length - 4), NetBitConverter.GetBytes(n));

                            return new OcbDecodeBucket(data, sessionKey, 128, sv, associatedData, verifyResult: x =>
                            {
                                if (!x)
                                    throw new BucketDecryptionException($"Verification of chunk {n + 1} in {data} bucket failed");
                            });
                        });

                    PushChunkReader(new CryptoChunkBucket(bucket.NoDispose()));
                    return false;
                }
            case CryptoTag.SymetricEncryptedIntegrity:
                {
                    byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                    var b = await StartDecrypt(bucket).ConfigureAwait(false);

                    PushChunkReader(new CryptoChunkBucket(b.NoDispose()));
                    return false;
                }
            case CryptoTag.UserID:
#if DEBUG
                {
                    var bb = await bucket.ReadAtLeastAsync(MaxRead, throwOnEndOfStream: false).ConfigureAwait(false);

                    Trace.WriteLine(bb.ToUTF8String());
                }
#endif
                break;
            case CryptoTag.Literal:
                {
                    byte dataType = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                    byte fileNameLen = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                    if (fileNameLen > 0)
                    {
                        var fn = await bucket.ReadAtLeastAsync(fileNameLen, throwOnEndOfStream: false).ConfigureAwait(false);
                        _fileName = fn.ToASCIIString();
                    }
                    else
                        _fileName = "";

                    var dt = await bucket.ReadAtLeastAsync(4, throwOnEndOfStream: false).ConfigureAwait(false); // Date
                    _fileDate = DateTimeOffset.FromUnixTimeSeconds(NetBitConverter.ToUInt32(dt, 0)).DateTime;

                    foreach (var s in _sigs)
                    {
                        var ha = s.HashAlgorithm;
                        bucket = CreateHasher(bucket, s.HashAlgorithm, a => s.Completer = a).NoDispose();
                    }

                    PushResultReader(bucket);
                    return false;
                }

            case CryptoTag.SymetricSessionKey:
                {
                    if (!_sessionKey.IsEmpty)
                    {
                        await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                        return true; // Ignore session key; we already have one
                    }

                    byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                    var cipherAlgorithm = (PgpSymmetricAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
                    if (version == 5)
                    {
                        byte ocb = (await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);

                        Debug.Assert(ocb == 2);
                    }

                    var s2k = await ReadPgpS2kSpecifierAsync(bucket, cipherAlgorithm).ConfigureAwait(false);

                    byte[]? key = null;
                    if (GetPassword?.Invoke(SignaturePromptContext.Empty) is { } password)
                    {
                        key = DeriveS2kKey(s2k, password);
                    }

                    if (key is not { })
                    {
                        await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                        return true; // Ignore session key
                    }

                    var symmetricKeyLength = cipherAlgorithm.GetKeySize() / 8;
                    switch (version)
                    {
                        case 5:
                            {
                                byte[] nonce = (await bucket.ReadAtLeastAsync(OcbDecodeBucket.MaxNonceLength, throwOnEndOfStream: false).ConfigureAwait(false)).ToArray(); // always 15, as OCB length

                                bool? verified_as_ok = null;

                                var ocb = new OcbDecodeBucket(bucket, key.ToArray(), 128, nonce,
                                    associatedData: new byte[] { (byte)(0xc0 | (int)tag), version, (byte)cipherAlgorithm, 0x02 /* s2k type */ },
                                    verifyResult: (result) => verified_as_ok = result);

                                var k = await ocb.ReadAtLeastAsync(symmetricKeyLength + 1, throwOnEndOfStream: false).ConfigureAwait(false);

#pragma warning disable CA1508 // Avoid dead conditional code // Bad diagnostic, as used in lambda.
                                Debug.Assert(verified_as_ok != null, "Verify not called");
                                if (k.Length == symmetricKeyLength && verified_as_ok == true)
#pragma warning restore CA1508 // Avoid dead conditional code
                                {
                                    _sessionAlgorithm = cipherAlgorithm;
                                    _sessionKey = k.ToArray();
                                }
                                break;
                            }
                        case 4 when (await bucket.ReadRemainingBytesAsync().ConfigureAwait(false) == 0):
                            {
                                // Use key directly
                                _sessionAlgorithm = cipherAlgorithm;
                                _sessionKey = key.ToArray();
                                break;
                            }

                        case 4:
                            {
                                await using var keySrc = CreateDecryptBucket(bucket, s2k.CipherAlgorithm, key, iv: new byte[key.Length]);

                                var k = await keySrc.ReadAtLeastAsync(key.Length + 2, throwOnEndOfStream: false).ConfigureAwait(false);

                                if (k.Length == key.Length - 1)
                                {
                                    _sessionAlgorithm = cipherAlgorithm;
                                    _sessionKey = k.Slice(1).ToArray();
                                }
                                break;
                            }
                        default:
                            await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                            break;
                    }

                }
                break;
            case CryptoTag.SignaturePublicKey:
                {
                    var r = await ParseSignatureAsync(bucket).ConfigureAwait(false);

                    var p = _sigs.Pop();

                    byte[] hashValue = p.Completer!(r.SignBlob)!;
                    //Trace.WriteLine(BucketExtensions.HashToString(hashValue));

                    if (KeyChain?.FindKey(r.SignKeyFingerprint) is PublicKeySignature key
                        && key.MatchFingerprint(r.SignKeyFingerprint) is { } matchedKey)
                    {
                        if (!VerifySignature(r, hashValue, matchedKey.GetValues(includePrivate: false)))
                        {
                            throw new BucketDecryptionException("SignaturePublicKey not verifiable");
                        }

                        _verifies++;
                    }

                    if (NetBitConverter.ToUInt16(hashValue, 0) != r.HashStart)
                        throw new BucketDecryptionException("Hashing towards SignaturePublicKey failed");
                }
                break;
            case CryptoTag.ModificationDetected:
                {
                    if (_literalSha is Interfaces.IBucketProduceHash completer)
                    {
                        completer.ProduceHash();

                        Debug.Assert(_shaResult != null);
                    }
                    _literalSha = null;

                    var bb = await bucket.ReadAtLeastAsync(20, throwOnEndOfStream: false).ConfigureAwait(false);

                    if (_shaResult is { } && !bb.Memory.SequenceEqual(_shaResult))
                        throw new BucketDecryptionException($"Modification detected in {nameof(CryptoTag.SymetricEncryptedIntegrity)} packet of {bucket} bucket");

                    _shaResult = null;

                    if (VerifySignature && _verifies == 0)
                        throw new BucketDecryptionException("Document signature was not verified");
                }
                break;
            default:
                await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                break;
        }

        {
            long n = await bucket.ReadUntilEofAsync().ConfigureAwait(false);
            Debug.Assert(n == 0, $"Unread data left in {bucket} of tagType {tag}");
        }
        return true;

    }

    private static (PgpHashAlgorithm hashAlgorithm, PgpSymmetricAlgorithm cipher, byte[] result) CreateEcdhParams(AsymmetricCryptoKey matchedKey, BigInteger kdf, ReadOnlyMemory<byte> oid)
    {
        ByteCollector ecdhParams;
        PgpHashAlgorithm kdfHash;
        PgpSymmetricAlgorithm kdfCipher;

        ecdhParams = new ByteCollector();
        ecdhParams.Append((byte)oid.Length);
        ecdhParams.Append(oid); // The OID
        ecdhParams.Append((byte)PgpPublicKeyType.ECDH);

        var kdfBytes = kdf.ToCryptoValue();

        // kdf[0] = 1 // Future extensibility
        kdfHash = (PgpHashAlgorithm)kdfBytes[1];
        kdfCipher = (PgpSymmetricAlgorithm)kdfBytes[2];
        ecdhParams.Append((byte)kdfBytes.Length);
        ecdhParams.Append(kdfBytes);
        ecdhParams.Append("Anonymous Sender    "u8.ToArray());
        ecdhParams.Append(matchedKey.Fingerprint.Slice(0, 20)); // Slice for v5 keys

        return (kdfHash, kdfCipher, ecdhParams.ToArray());
    }

    private void SetupSessionFromDecrypted(byte[] data)
    {
        var alg = (PgpSymmetricAlgorithm)data[0];

        var dataLen = alg.GetKeySize() / 8;

        ushort checksum = NetBitConverter.ToUInt16(data, 1 + dataLen);

        if (checksum == data.Skip(1).Take(dataLen).Sum(x => x))
        {
            _sessionAlgorithm = alg;
            _sessionKey = data.AsMemory(1, dataLen); // Minus first byte (session alg) and last two bytes (checksum)
        }
    }

    private async ValueTask<Bucket> StartDecrypt(Bucket bucket)
    {
        RawDecryptBucket dcb;
        switch (_sessionAlgorithm)
        {
            case PgpSymmetricAlgorithm.Aes:
            case PgpSymmetricAlgorithm.Aes192:
            case PgpSymmetricAlgorithm.Aes256:
                {
                    var aes = Aes.Create();
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
                    aes.Mode = CipherMode.CFB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
                    aes.Key = _sessionKey.ToArray();
                    aes.IV = new byte[aes.BlockSize / 8];
                    aes.Padding = PaddingMode.None;
                    aes.FeedbackSize = aes.BlockSize;

                    dcb = new RawDecryptBucket(bucket, aes.ApplyModeShim(), decrypt: true);

                    break;
                }
            case PgpSymmetricAlgorithm.TripleDes:
                {
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
                    var tripleDes = TripleDES.Create();
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
                    tripleDes.Mode = CipherMode.CFB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
                    tripleDes.Key = _sessionKey.ToArray();
                    tripleDes.IV = new byte[tripleDes.BlockSize / 8];
                    tripleDes.Padding = PaddingMode.None;
                    tripleDes.FeedbackSize = tripleDes.BlockSize;

                    dcb = new RawDecryptBucket(bucket, tripleDes.ApplyModeShim(), decrypt: true);

                    break;
                }
            default:
                throw new NotSupportedException($"Decrypt for cipher {_sessionAlgorithm} not implemented yet.");
        }

        _literalSha = dcb.SHA1((value) => _shaResult = value);

        var bb = await _literalSha.ReadAtLeastAsync(dcb.BlockBytes + 2, throwOnEndOfStream: false).ConfigureAwait(false);

        if (bb.Length != dcb.BlockBytes + 2 || bb[bb.Length - 1] != bb[bb.Length - 3] || bb[bb.Length - 2] != bb[bb.Length - 4])
            throw new InvalidOperationException("Decrypt failed. Wrong session key");

        return _literalSha;
    }

    public async ValueTask<(string? fileName, DateTime? fileTime)> ReadFileInfo()
    {
        if (_fileName != null)
            await ReadHeaderChunksAsync().ConfigureAwait(false);

        return (_fileName, _fileDate);
    }

    public override bool CanReset => Source.CanReset;

    public override void Reset()
    {
        base.Reset();
        _sigs.Clear();
        _verifies = 0;
    }

    private sealed record PgpSignature
    {
        public PgpSignature(byte version, PgpSignatureType signatureType, PgpHashAlgorithm hashAlgorithm, PgpPublicKeyType pkt, byte[] signer)
        {
            Version = version;
            SignatureType = signatureType;
            HashAlgorithm = hashAlgorithm;
            Pkt = pkt;
            Signer = signer;
        }

        public byte Version { get; }
        public PgpSignatureType SignatureType { get; }
        public PgpHashAlgorithm HashAlgorithm { get; }
        public PgpPublicKeyType Pkt { get; }
        public byte[] Signer { get; }
        public Func<byte[]?, byte[]>? Completer { get; internal set; }
    }
}
