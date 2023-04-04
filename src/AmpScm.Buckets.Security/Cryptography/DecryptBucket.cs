using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;

// https://www.rfc-editor.org/rfc/rfc4880
// https://datatracker.ietf.org/doc/draft-koch-openpgp-2015-rfc4880bis/

namespace AmpScm.Buckets.Cryptography;

public sealed class DecryptBucket : CryptoDataBucket
{
    private OpenPgpSymmetricAlgorithm _sessionAlgorithm;
    private ReadOnlyMemory<byte> _sessionKey; // The symetric key
    private string? _fileName;
    private DateTime? _fileDate;
    private readonly Stack<PgpSignature> _sigs = new();

    public DecryptBucket(Bucket source)
        : base(new CryptoChunkBucket(source))
    {
    }

    public Func<SignaturePromptContext, string>? GetPassword { get; init; }

    private protected override async ValueTask<bool> HandleChunk(Bucket bucket, CryptoTag tag)
    {
        switch (tag)
        {
            case CryptoTag.CompressedData:
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

                    PushChunkReader(new CryptoChunkBucket(rd.NoDispose()));
                    return false;
                }

            case CryptoTag.PublicKeySession:
                {
                    byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                    // Read the public key, for who the file is encrypted
                    var bb = (await bucket.ReadExactlyAsync(8).ConfigureAwait(false));

                    if (!_sessionKey.IsEmpty)
                    {
                        await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                        break; // Skip packet, we already have a session
                    }

                    var fingerprint = bb.ToArray();
                    var pca = (OpenPgpPublicKeyType)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);

                    var key = KeyChain?.FindKey(fingerprint, requirePrivateKey: true) as PublicKeySignature;

                    if (!(key?.HasPrivateKey ?? false) || key?.MatchFingerprint(fingerprint) is not { } matchedKey)
                    {
                        if (KeyChain?.FirstOrDefault(x => x.MatchesFingerprint(bb)) is { } kk
                            && (kk as PublicKeySignature)?.MatchFingerprint(fingerprint) is { } mk
                            && mk.HasPrivateKey)
                        {
                            matchedKey = mk;
                        }
                        else
                        {
                            await bucket.ReadUntilEofAsync().ConfigureAwait(false);
                            break; // Ignore rest of packet
                        }
                    }

                    var keyValues = matchedKey.GetValues(false);

                    switch (matchedKey.Algorithm)
                    {
                        case CryptoAlgorithm.Rsa:
                            using (var rsa = RSA.Create())
                            {
                                rsa.ImportParametersFromCryptoInts(keyValues);

                                var bi = await ReadPgpMultiPrecisionInteger(bucket).ConfigureAwait(false);

                                byte[] data = rsa.Decrypt(bi.Value.ToCryptoValue(), RSAEncryptionPadding.Pkcs1);
                                ushort checksum = NetBitConverter.ToUInt16(data, data.Length - 2);

                                if (checksum == data.Skip(1).Take(data.Length - 3).Sum(x => (ushort)x))
                                {
                                    _sessionAlgorithm = (OpenPgpSymmetricAlgorithm)data[0];
                                    _sessionKey = data.AsMemory(1, data.Length - 3); // Minus first byte (session alg) and last two bytes (checksum)
                                }
                            }
                            break;

                        case CryptoAlgorithm.Ecdh:
                            using (var ecdh = ECDiffieHellman.Create())
                            {
                                ecdh.ImportParametersFromCryptoInts(keyValues);

                                var bi1 = await ReadPgpMultiPrecisionInteger(bucket).ConfigureAwait(false);
                                var b = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                                var kdf = await bucket.ReadExactlyAsync(b).ConfigureAwait(false);

                                var kb = kdf.Memory.AsBucket();

                                b = await kb.ReadByteAsync().ConfigureAwait(false) ?? 0;

                                //var sd = await kb.ReadExactlyAsync(b).ConfigureAwait(false);
                                //
                                //b = await kb.ReadByteAsync().ConfigureAwait(false) ?? 0;
                                //
                                //var sd2 = await kb.ReadExactlyAsync(b).ConfigureAwait(false);
                                //
                                //byte[] data = null!;// ecdh.dec.Decrypt(bi.Value.ToCryptoValue(), RSAEncryptionPadding.Pkcs1);
                            }
                            break;

                        case CryptoAlgorithm.Curve25519:


                        default:
                            throw new NotImplementedException($"Algorithm {matchedKey.Algorithm} not implemented yet");
                    }
                }
                break;
            case CryptoTag.OnePassSignature:
                {
                    byte version = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
                    var signatureType = (OpenPgpSignatureType)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
                    var hashAlgorithm = (OpenPgpHashAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
                    var pkt = (OpenPgpPublicKeyType)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);

                    byte[] signer = (await bucket.ReadExactlyAsync(8).ConfigureAwait(false)).ToArray();

                    byte flag = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                    GC.KeepAlive(flag);

                    _sigs.Push(new(version, signatureType, hashAlgorithm, pkt, signer));
                }
                break;
            case CryptoTag.OCBEncryptedData:
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
                    var sessionKey = _sessionKey.ToArray();

                    // Encrypted data

                    bucket = bucket.Leave(16, async (bb, length) =>
                    {
                        long n = (length + chunk_size - 1) / chunk_size; // Calculate number of chunks before the final block
                        length -= n * 16; // Remove the tags of these chunks from the total length

                        byte[] associatedData = new byte[] { 0xC0 | (int)CryptoTag.OCBEncryptedData, version, (byte)cipherAlgorithm, aeadAlgorithm, chunkVal }
                                .Concat(NetBitConverter.GetBytes((long)n)).Concat(NetBitConverter.GetBytes((long)length)).ToArray();

                        byte[] sv = startingVector.ToArray();
                        OcbDecodeBucket.SpanXor(sv.AsSpan(sv.Length - 4), NetBitConverter.GetBytes((int)n));

                        using var ocd = new OcbDecodeBucket(bb.Memory.AsBucket(), sessionKey, 128, sv, associatedData, verifyResult: x =>
                        {
                            if (!x)
                                throw new BucketDecryptionException($"Verification of final chunk in {bucket} bucket failed");
                        });

                        await ocd.ReadUntilEofAsync().ConfigureAwait(false);
                    });

                    bucket = new AeadChunkReader(bucket, (int)chunk_size, 16,
                        (n, data) =>
                        {
                            byte[] associatedData = new byte[] { 0xC0 | (int)CryptoTag.OCBEncryptedData, version, (byte)cipherAlgorithm, aeadAlgorithm, chunkVal }
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
                    var bb = await bucket.ReadExactlyAsync(MaxRead).ConfigureAwait(false);

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
                        var fn = await bucket.ReadExactlyAsync(fileNameLen).ConfigureAwait(false);
                        _fileName = fn.ToASCIIString();
                    }
                    else
                        _fileName = "";

                    var dt = await bucket.ReadExactlyAsync(4).ConfigureAwait(false); // Date
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
                    var cipherAlgorithm = (OpenPgpSymmetricAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
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

                    var symmetricKeyLength = GetKeySize(cipherAlgorithm) / 8;
                    switch (version)
                    {
                        case 5:
                            {
                                byte[] nonce = (await bucket.ReadExactlyAsync(OcbDecodeBucket.MaxNonceLength).ConfigureAwait(false)).ToArray(); // always 15, as OCB length

                                bool? verified_as_ok = null;

                                var ocb = new OcbDecodeBucket(bucket, key.ToArray(), 128, nonce,
                                    associatedData: new byte[] { (byte)(0xc0 | (int)tag), version, (byte)cipherAlgorithm, 0x02 /* s2k type */ },
                                    verifyResult: (result) => verified_as_ok = result);

                                var k = await ocb.ReadExactlyAsync(symmetricKeyLength + 1).ConfigureAwait(false);

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
                                _sessionAlgorithm = cipherAlgorithm;
                                _sessionKey = key.ToArray();
                                break;
                            }

                        case 4:
                            {
                                using var keySrc = CreateDecryptBucket(bucket, s2k.CipherAlgorithm, key, iv: new byte[key.Length]);

                                var k = await keySrc.ReadExactlyAsync(key.Length + 2).ConfigureAwait(false);

                                if (k.Length == key.Length - 1)
#pragma warning restore CA1508 // Avoid dead conditional code
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
                        if (!VerifySignature(r, hashValue, matchedKey.GetValues(false)))
                        {
                            throw new BucketDecryptionException("SignaturePublicKey not verifiable");
                        }
                    }

                    if (NetBitConverter.ToUInt16(hashValue, 0) != r.HashStart)
                        throw new BucketDecryptionException("Hashing towards SignaturePublicKey failed");
                }
                break;
            case CryptoTag.ModificationDetected:
                {
                    var bb = await bucket.ReadExactlyAsync(20).ConfigureAwait(false);
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

    private async ValueTask<Bucket> StartDecrypt(Bucket bucket)
    {
        switch (_sessionAlgorithm)
        {
            case OpenPgpSymmetricAlgorithm.Aes:
            case OpenPgpSymmetricAlgorithm.Aes192:
            case OpenPgpSymmetricAlgorithm.Aes256:
                {
                    var aes = Aes.Create();
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
                    aes.Mode = CipherMode.CFB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
                    aes.Key = _sessionKey.ToArray();
                    aes.IV = new byte[aes.BlockSize / 8];
                    aes.Padding = PaddingMode.None;
                    aes.FeedbackSize = aes.BlockSize;

                    var dcb = new RawDecryptBucket(bucket, aes.ApplyModeShim(), true);

                    var bb = await dcb.ReadExactlyAsync(aes.BlockSize / 8 + 2).ConfigureAwait(false);

                    if (bb[bb.Length - 1] != bb[bb.Length - 3] || bb[bb.Length - 2] != bb[bb.Length - 4])
                        throw new InvalidOperationException("AES-256 decrypt failed");

                    return dcb;
                }
            case OpenPgpSymmetricAlgorithm.TripleDes:
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

                    var dcb = new RawDecryptBucket(bucket, tripleDes.ApplyModeShim(), true);

                    var bb = await dcb.ReadExactlyAsync(tripleDes.BlockSize / 8 + 2).ConfigureAwait(false);

                    if (bb[bb.Length - 1] != bb[bb.Length - 3] || bb[bb.Length - 2] != bb[bb.Length - 4])
                        throw new InvalidOperationException("TripleDes decrypt failed");

                    return dcb;
                }
            default:
                throw new NotImplementedException($"Decrypt for cipher {_sessionAlgorithm} not implemented yet.");
        }
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
    }

    private sealed record PgpSignature
    {
        public PgpSignature(byte version, OpenPgpSignatureType signatureType, OpenPgpHashAlgorithm hashAlgorithm, OpenPgpPublicKeyType pkt, byte[] signer)
        {
            Version = version;
            SignatureType = signatureType;
            HashAlgorithm = hashAlgorithm;
            Pkt = pkt;
            Signer = signer;
        }

        public byte Version { get; }
        public OpenPgpSignatureType SignatureType { get; }
        public OpenPgpHashAlgorithm HashAlgorithm { get; }
        public OpenPgpPublicKeyType Pkt { get; }
        public byte[] Signer { get; }
        public Func<byte[]?, byte[]>? Completer { get; internal set; }
    }
}
