using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Cryptography;


public abstract class CryptoDataBucket : WrappingBucket
{
    readonly private Stack<CryptoChunkBucket> _stack = new();
    private Bucket? _reader;
    private long _position;

    private protected new CryptoChunkBucket Source => (CryptoChunkBucket)base.Source;

    public CryptoKeyChain KeyChain { get; init; } = new CryptoKeyChain();

    public Func<SignaturePromptContext, string>? GetPassword { get; init; }

    private protected CryptoDataBucket(CryptoChunkBucket source) : base(source)
    {
        if (Source is { })
            PushChunkReader(Source);
    }

    private protected record struct S2KSpecifier(PgpHashAlgorithm HashAlgorithm, byte[]? Salt, int HashByteCount, PgpSymmetricAlgorithm CipherAlgorithm, byte Type);

    private protected static byte[] DeriveS2kKey(S2KSpecifier s2k, string password)
    {
        int bitsRequired = s2k.CipherAlgorithm.GetKeySize();

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

            var zeroBytes = (zeros > 0) ? Enumerable.Range(0, zeros).Select(_ => (byte)0).ToArray() : null;
            zeros++;

            byte[] toHash = pwd.ToArray();

            using HashAlgorithm ha = CreatePgpHashAlgorithm(s2k.HashAlgorithm);

            if (s2k.HashByteCount <= toHash.Length)
            {
                result.AddRange(ha.ComputeHash(zeroBytes is null ? toHash : zeroBytes.Concat(toHash).ToArray()));
                continue;
            }

            if (zeroBytes is { })
                ha.TransformBlock(zeroBytes, 0, zeroBytes.Length, outputBuffer: null, 0);

            int nHashBytes = s2k.HashByteCount;
            do
            {
                int n = Math.Min(nHashBytes, toHash.Length);
                ha.TransformBlock(toHash, 0, n, outputBuffer: null, 0);

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

#if NET
            string b64 = Convert.ToBase64String(fingerprint.Span);
#else
            string b64 = Convert.ToBase64String(fingerprint.ToArray());
#endif

            return $"{Encoding.ASCII.GetString(vals[0].ToCryptoValue())} {b64}";
        }

        return "";
    }

    internal static BigInteger[] GetEcdsaValues(IReadOnlyList<BigInteger> vals)
    {
        var curveValue = vals[0].ToCryptoValue();

#pragma warning disable CA1307 // Specify StringComparison for clarity
        string name = Encoding.ASCII.GetString(curveValue).Replace('p', 'P').Replace("Pool", "pool");
#pragma warning restore CA1307 // Specify StringComparison for clarity


        switch (name)
        {
            case nameof(ECCurve.NamedCurves.nistP256):
                curveValue = new byte[] { 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07 };
                break;
            case nameof(ECCurve.NamedCurves.nistP384):
                curveValue = new byte[] { 0x2B, 0x81, 0x04, 0x00, 0x22 };
                break;
            case nameof(ECCurve.NamedCurves.nistP521):
                curveValue = new byte[] { 0x2B, 0x81, 0x04, 0x00, 0x23 };
                break;
            case nameof(ECCurve.NamedCurves.brainpoolP256r1):
                curveValue = new byte[] { 0x2B, 0x24, 0x03, 0x03, 0x02, 0x08, 0x01, 0x01, 0x07 };
                break;
            case nameof(ECCurve.NamedCurves.brainpoolP384r1):
                curveValue = new byte[] { 0x2B, 0x24, 0x03, 0x03, 0x02, 0x08, 0x01, 0x01, 0x08 };
                break;
            case nameof(ECCurve.NamedCurves.brainpoolP512t1):
                curveValue = new byte[] { 0x2B, 0x24, 0x03, 0x03, 0x02, 0x08, 0x01, 0x01, 0x0D };
                break;
            // Ed24419
            // Curve25519
            // Ed448
            // X448
            default:
                throw new NotSupportedException($"Unknown curve {name}");
        }

        return new[] { curveValue.ToBigInteger() }.Concat(vals.Skip(1)).ToArray();
    }

    internal static BigInteger[] ParseSshStrings(ReadOnlyMemory<byte> data)
    {
        List<BigInteger> mems = new();

        // HACK^2: We know we have a memory only bucket, so ignore everything async
        // And we also know the result will refer to the original data, so returning
        // references is safe in this specific edge case.

        var b = data.AsBucket();

        while (ReadSshStringAsync(b).AsTask().Result is BucketBytes bb && !bb.IsEof)
        {
            mems.Add(bb.Memory.ToBigInteger());

            //Debug.Assert(bb.Memory.ToBigInteger().ToCryptoValue(false).AsSpan().SequenceEqual(bb.Memory.Span));
        }

        return mems.ToArray();
    }

    private protected static async ValueTask<S2KSpecifier> ReadPgpS2kSpecifierAsync(Bucket bucket, PgpSymmetricAlgorithm algorithm)
    {
        byte type = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;
        PgpHashAlgorithm alg;
        byte[] salt;

        switch (type)
        {
            case 0:
                { // Simple S2K
                    alg = (PgpHashAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);

                    return new(alg, Salt: null, 0, algorithm, type);
                }
            case 1:
                { // Salted S2k
                    alg = (PgpHashAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
                    salt = (await bucket.ReadAtLeastAsync(8, throwOnEndOfStream: false).ConfigureAwait(false)).ToArray();

                    return new(alg, salt, 0, algorithm, type);
                }
            // 2 : reserved
            case 3:
                { // Iterated and Salted S2K
                    alg = (PgpHashAlgorithm)(await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0);
                    salt = (await bucket.ReadAtLeastAsync(8, throwOnEndOfStream: false).ConfigureAwait(false)).ToArray();
                    int count = await bucket.ReadByteAsync().ConfigureAwait(false) ?? 0;

                    int c = 16 + (count & 0xF) << ((count >> 4) + 6);

                    return new(alg, salt, c, algorithm, type);
                }
            default:
                throw new NotSupportedException($"Unknow S2k specifier {type}");
        }
    }



    private protected static async ValueTask<byte[]> CalculateHash(Bucket sourceData, PgpHashAlgorithm hashAlgorithm)
    {
        byte[]? result = null;
        await using var sd = sourceData.Hash(CreatePgpHashAlgorithm(hashAlgorithm), x => result = x);

        await sd.ReadUntilEofAsync().ConfigureAwait(false);

#pragma warning disable CA1508 // Avoid dead conditional code
        return result ?? throw new InvalidOperationException();
#pragma warning restore CA1508 // Avoid dead conditional code
    }

    private protected static HashAlgorithm CreatePgpHashAlgorithm(PgpHashAlgorithm hashAlgorithm)
    {
        return hashAlgorithm switch
        {
            PgpHashAlgorithm.SHA256 => SHA256.Create(),
            PgpHashAlgorithm.SHA384 => SHA384.Create(),
            PgpHashAlgorithm.SHA512 => SHA512.Create(),
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
            PgpHashAlgorithm.SHA1 => SHA1.Create(),
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
            PgpHashAlgorithm.MD5 => MD5.Create(),
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms

#if NETFRAMEWORK
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
            PgpHashAlgorithm.MD160 => RIPEMD160.Create(),
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
#endif

            _ => throw new NotSupportedException($"Hash algorithm {hashAlgorithm} is not supported.")
        };
    }

    private protected static Bucket CreateHasher(Bucket? bucket, PgpHashAlgorithm hashAlgorithm, Action<Func<byte[]?, byte[]>> completer)
    {
        if (bucket is null)
            throw new ArgumentNullException(nameof(bucket));
        else if (completer is null)
            throw new ArgumentNullException(nameof(completer));

        return bucket.Hash(CreatePgpHashAlgorithm(hashAlgorithm), completer);
    }

    private protected static CryptoAlgorithm GetCryptoAlgorithm(PgpPublicKeyType keyPublicKeyType)
    {
        return keyPublicKeyType switch
        {
            PgpPublicKeyType.Rsa => CryptoAlgorithm.Rsa,
            PgpPublicKeyType.Dsa => CryptoAlgorithm.Dsa,
            PgpPublicKeyType.ECDSA => CryptoAlgorithm.Ecdsa,
            PgpPublicKeyType.Ed25519 => CryptoAlgorithm.Ed25519,
            PgpPublicKeyType.ECDH => CryptoAlgorithm.Ecdh,
            PgpPublicKeyType.Curve25519 => CryptoAlgorithm.Curve25519,
            PgpPublicKeyType.Elgamal => CryptoAlgorithm.Elgamal,
            _ => throw new ArgumentOutOfRangeException(nameof(keyPublicKeyType), keyPublicKeyType, message: null)
        };
    }

    private protected static async ValueTask<uint?> ReadLengthAsync(Bucket bucket)
    {
        return (await CryptoChunkBucket.ReadLengthAsync(bucket).ConfigureAwait(false)).Length;
    }

    private protected static async ValueTask<BucketBytes> ReadSshStringAsync(Bucket bucket)
    {
        var bb = await bucket.ReadAtLeastAsync(sizeof(int), throwOnEndOfStream: false).ConfigureAwait(false);

        if (bb.IsEof)
            return BucketBytes.Eof;
        else if (bb.Length < sizeof(int))
            throw new BucketEofException(bucket);

        int len = NetBitConverter.ToInt32(bb, 0);

        if (len == 0)
            return BucketBytes.Empty;

        return await bucket.ReadAtLeastAsync(len, throwOnEndOfStream: false).ConfigureAwait(false);
    }

    private protected static async ValueTask<BigInteger?> ReadPgpMultiPrecisionInteger(Bucket sourceData)
    {
        var bb = await sourceData.ReadAtLeastAsync(2, false).ConfigureAwait(false);

        if (bb.IsEof)
            return null;

        ushort bitLen = NetBitConverter.ToUInt16(bb, 0);

        if (bitLen == 0)
            return null;
        else
        {
            int byteLen = (bitLen + 7) / 8;
            bb = await sourceData.ReadAtLeastAsync(byteLen).ConfigureAwait(false);

            return bb.Memory.ToBigInteger();
        }
    }

    private protected static bool SplitSignatureInt(int index, PgpPublicKeyType signaturePublicKeyType)
    {
        return signaturePublicKeyType == PgpPublicKeyType.ECDSA && index == 0;
    }

    private protected static async ValueTask SequenceToList(List<BigInteger> values, DerBucket der2, CryptoAlgorithm cryptoAlgorithm)
    {
        while (true)
        {
            var (b, bt) = await der2.ReadValueAsync().ConfigureAwait(false);

            if (b is null)
                break;

            switch (bt)
            {
                case DerType.Sequence:
                    {
                        await using var bq = new DerBucket(b);
                        await SequenceToList(values, bq, cryptoAlgorithm).ConfigureAwait(false);
                        break;
                    }
                case DerType.Null:
                    await b.ReadUntilEofAndCloseAsync().ConfigureAwait(false);
                    break;
                case DerType.BitString when cryptoAlgorithm is CryptoAlgorithm.Rsa or CryptoAlgorithm.Dsa:
                    {
                        var bs = await b.ReadByteAsync().ConfigureAwait(false);

                        Debug.Assert(bs == 0);

                        await using var bq = new DerBucket(b);
                        await SequenceToList(values, bq, cryptoAlgorithm).ConfigureAwait(false);
                        break;
                    }
                case DerType.ObjectIdentifier:
                    {
                        var bb = await b!.ReadAtLeastAsync(32768, throwOnEndOfStream: false).ConfigureAwait(false);
                        values.Add(bb.Memory.ToBigInteger());
                    }
                    break;
                default:
                    {
                        var bb = await b!.ReadAtLeastAsync(32768, throwOnEndOfStream: false).ConfigureAwait(false);

                        values.Add(bb.Memory.ToBigInteger());

                        break;
                    }
            }
        }
    }


    private protected static Bucket CreateDecryptBucket(Bucket source, PgpSymmetricAlgorithm algorithm, byte[] key, byte[]? iv = null)
    {
        switch (algorithm)
        {
            case PgpSymmetricAlgorithm.Aes:
            case PgpSymmetricAlgorithm.Aes192:
            case PgpSymmetricAlgorithm.Aes256:

                var aes = Aes.Create();
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
                aes.Mode = CipherMode.CFB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
                aes.KeySize = algorithm.GetKeySize();
                aes.Key = key;
                aes.IV = iv ?? new byte[algorithm.GetBlockBytes()];
                aes.Padding = PaddingMode.None;
                aes.FeedbackSize = aes.BlockSize;

                return new RawDecryptBucket(source, aes.ApplyModeShim(), decrypt: true);

            default:
                throw new NotSupportedException($"Not implemented for {algorithm} algorithm yet");
        }
    }

    private protected sealed record SignatureInfo(PgpSignatureType SignatureType, byte[]? Signer, PgpPublicKeyType PublicKeyType, PgpHashAlgorithm HashAlgorithm, ushort HashStart, DateTimeOffset? SignTime, byte[]? SignBlob, IReadOnlyList<BigInteger> SignatureInts, byte[]? SignKeyFingerprint);


    public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
    {
        do
        {
            if (_reader != null)
            {
                var bb = await _reader.ReadAsync(requested).ConfigureAwait(false);

                if (!bb.IsEmpty)
                {
                    _position += bb.Length;
                    return bb;
                }
                else
                {
                    await _reader.DisposeAsync();
                    _reader = null;
                }
            }

            await ReadHeaderChunksAsync().ConfigureAwait(false);
        }
        while (_reader != null);

        return BucketBytes.Eof;
    }

    private protected async ValueTask ReadHeaderChunksAsync()
    {
        if (_reader != null)
            return;

        while (_stack.Count > 0)
        {
            var rdr = _stack.Peek();
            var (bucket, type) = await rdr.ReadChunkAsync().ConfigureAwait(false);

            if (bucket is { })
            {
                if (await HandleChunk(bucket, type).ConfigureAwait(false))
                    await bucket.DisposeAsync();

                if (_reader != null)
                    break; // Jump to returning actual data
            }
            else
            {
                _stack.Pop();
                if (rdr != Source)
                    await rdr.DisposeAsync();
            }
        }
    }

    public override BucketBytes Peek()
    {
        if (_reader is { })
            return _reader.Peek();
        else
            return BucketBytes.Empty;
    }

    private protected void PushChunkReader(CryptoChunkBucket bucket)
    {
        _stack.Push(bucket);
    }

    private protected void PushResultReader(Bucket bucket)
    {
        if (_reader != null)
            throw new InvalidOperationException();

        _reader = bucket;
    }

    private protected abstract ValueTask<bool> HandleChunk(Bucket bucket, CryptoTag type);

#pragma warning disable MA0051 // Method is too long
    private protected static async ValueTask<SignatureInfo> ParseSignatureAsync(Bucket bucket)
#pragma warning restore MA0051 // Method is too long
    {
        PgpSignatureType signatureType;
        byte[]? signer = null;
        PgpPublicKeyType publicKeyType;
        PgpHashAlgorithm hashAlgorithm;
        ushort hashStart;
        DateTime? signTime = null;
        byte[]? signBlob;
        BigInteger[]? signatureInts;
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

            var bb = await bucket.ReadAtLeastAsync(hdrLen, throwOnEndOfStream: false).ConfigureAwait(false);

            if (bb.Length != hdrLen)
                throw new BucketEofException(bucket);

            ByteCollector bc = new ByteCollector(1024);
            bc.Append(version);
            bc.Append(bb);

            signatureType = (PgpSignatureType)bb[0];
            publicKeyType = (PgpPublicKeyType)bb[1];
            hashAlgorithm = (PgpHashAlgorithm)bb[2];
            int subLen;

            if (version == 4)
                subLen = NetBitConverter.ToUInt16(bb, 3);
            else
                subLen = (int)NetBitConverter.ToUInt32(bb, 3);

            if (subLen > 0)
            {
                var subRead = bucket.TakeExactly(subLen)
                    .AtRead(bb =>
                    {
                        if (!bb.IsEmpty)
                            bc.Append(bb);

                        return new();
                    });

                (signTime, signer, signKeyFingerprint) = await ParseSubPacketsAsync(subRead, hashed: true, signTime, signer, signKeyFingerprint).ConfigureAwait(false);
            }

            uint unhashedLen;

            if (version == 4)
                unhashedLen = await bucket.ReadNetworkUInt16Async().ConfigureAwait(false);
            else
                unhashedLen = await bucket.ReadNetworkUInt32Async().ConfigureAwait(false);

            if (unhashedLen > 0)
            {
                var subRead = bucket.TakeExactly(unhashedLen);

                (signTime, signer, signKeyFingerprint) = await ParseSubPacketsAsync(subRead, hashed: false, signTime, signer, signKeyFingerprint).ConfigureAwait(false);
            }

            // First 2 bytes of hash
            hashStart = await bucket.ReadNetworkUInt16Async().ConfigureAwait(false);

            if (version != 4)
            {
                // In v5, 16 bytes of salt
                bb = await bucket.ReadAtLeastAsync(16, throwOnEndOfStream: false).ConfigureAwait(false);
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
            var bb = await bucket.ReadAtLeastAsync(18, throwOnEndOfStream: false).ConfigureAwait(false);
            if (bb[0] != 5)
                throw new BucketException($"HashInfoLen must by 5 for v3 in {bucket.Name}");
            signatureType = (PgpSignatureType)bb[1];
            signTime = DateTimeOffset.FromUnixTimeSeconds(NetBitConverter.ToUInt32(bb, 2)).DateTime;
            signer = bb.Slice(6, 8).ToArray();
            publicKeyType = (PgpPublicKeyType)bb[14];
            hashAlgorithm = (PgpHashAlgorithm)bb[15];
            hashStart = NetBitConverter.ToUInt16(bb, 16);

            signBlob = bb.Slice(1, 5).ToArray();
        }
        else
            throw new NotSupportedException("Only OpenPGP SignaturePublicKey versions 3, 4 and 5 are supported");

        List<BigInteger> bigInts = new();
        while (await ReadPgpMultiPrecisionInteger(bucket).ConfigureAwait(false) is { } bi)
        {
            bigInts.Add(bi);
        }

        signatureInts = bigInts.ToArray();

        if (publicKeyType == PgpPublicKeyType.EdDSA && signatureInts.Length == 2 /* signatureInts.All(x => x.Length == 32)*/)
        {
            publicKeyType = PgpPublicKeyType.Ed25519;
            signatureInts = new[] { signatureInts.SelectMany(x => x.ToCryptoValue()).ToBigInteger() };
        }
        else if (publicKeyType == PgpPublicKeyType.Dsa)
        {
            signatureInts = new[] { signatureInts.SelectMany(x => x.ToCryptoValue()).ToBigInteger() };
        }

        return new(signatureType, signer, publicKeyType, hashAlgorithm, hashStart, signTime, signBlob, signatureInts, signKeyFingerprint);
    }

    private static async ValueTask<(DateTime? signTime, byte[]? signer, byte[]? signKeyFingerprint)> ParseSubPacketsAsync(Bucket subRead, bool hashed, DateTime? signTime, byte[]? signer, byte[]? signKeyFingerprint)
    {
        while (true)
        {
            uint? len = await ReadLengthAsync(subRead).ConfigureAwait(false);

            if (!len.HasValue)
                break;

            byte b = await subRead.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(subRead);
            len--;

            switch ((PgpSubPacketType)b)
            {
                case PgpSubPacketType.SignatureCreationTime:
                    if (len != 4)
                        throw new InvalidOperationException();

                    int time = await subRead.ReadNetworkInt32Async().ConfigureAwait(false);

                    signTime = DateTimeOffset.FromUnixTimeSeconds(time).DateTime;
                    break;
                case PgpSubPacketType.Issuer:
                    signer = (await subRead.ReadAtLeastAsync((int)len, throwOnEndOfStream: false).ConfigureAwait(false)).ToArray();
                    break;
                case PgpSubPacketType.IssuerFingerprint:
                    signKeyFingerprint = (await subRead.ReadAtLeastAsync((int)len, throwOnEndOfStream: false).ConfigureAwait(false)).ToArray();
                    break;

                // Currently unhandled fields from private keys
                case PgpSubPacketType.KeyFlags:
                case PgpSubPacketType.KeyExpirationTime:
                case PgpSubPacketType.PreferredSymetricAlgorithms:
                case PgpSubPacketType.PreferredHashAlgorithms:
                case PgpSubPacketType.PreferredCompressionAlgorithms:
                case PgpSubPacketType.Features:
                case PgpSubPacketType.KeyServerPreferences:
                case PgpSubPacketType.PreferredEncryptionModes:
                    if (len != await subRead.ReadSkipAsync(len.Value).ConfigureAwait(false))
                        throw new BucketEofException(subRead);
                    break;

                case PgpSubPacketType.SignersUserID:
                    if (len != await subRead.ReadSkipAsync(len.Value).ConfigureAwait(false))
                        throw new BucketEofException(subRead);
                    break;
                default:
                    if (len != await subRead.ReadSkipAsync(len.Value).ConfigureAwait(false))
                        throw new BucketEofException(subRead);
                    break;
            }
        }

        return (signTime, signer, signKeyFingerprint);
    }

    private protected static bool VerifySignature(SignatureInfo signatureInfo, byte[] hashValue, IReadOnlyList<BigInteger> keyValues)
    {
        if (signatureInfo.SignatureInts == null)
            throw new InvalidOperationException("No SignaturePublicKey value found to verify against");

        switch (signatureInfo.PublicKeyType)
        {
            case PgpPublicKeyType.Rsa:

                using (var rsa = RSA.Create())
                {
                    rsa.ImportParametersFromCryptoValues(keyValues);

                    var SignaturePublicKey = signatureInfo.SignatureInts![0];

                    return rsa.VerifyHash(hashValue, SignaturePublicKey.ToCryptoValue(), signatureInfo.HashAlgorithm.GetHashAlgorithmName(), RSASignaturePadding.Pkcs1);
                }
            case PgpPublicKeyType.Dsa:
                using (var dsa = DSA.Create())
                {
                    var SignaturePublicKey = signatureInfo.SignatureInts![0];

                    dsa.ImportParametersFromCryptoValues(keyValues);

                    return dsa.VerifySignature(hashValue, SignaturePublicKey.ToCryptoValue());
                }
            case PgpPublicKeyType.ECDSA:
                using (var ecdsa = ECDsa.Create())
                {
                    ecdsa.ImportParametersFromCryptoInts(keyValues);

                    byte[] r = signatureInfo.SignatureInts[0].ToCryptoValue().AlignUp(2);
                    byte[] s = signatureInfo.SignatureInts[1].ToCryptoValue().AlignUp(2);

                    int klen = ecdsa.KeySize switch
                    {
                        256 => 32,
                        384 => 48,
                        521 => 66,
                        _ => Math.Max(r.Length, s.Length)
                    };

                    byte[] sig = new byte[2 * klen];

                    r.CopyTo(sig, klen - r.Length);
                    s.CopyTo(sig, 2 * klen - s.Length);

                    return ecdsa.VerifyHash(hashValue, sig);
                }
            case PgpPublicKeyType.Ed25519:
                {
                    byte[] SignaturePublicKey = signatureInfo.SignatureInts![0].ToCryptoValue();

                    return Chaos.NaCl.Ed25519.Verify(SignaturePublicKey, hashValue, keyValues[0].ToCryptoValue());
                }
            case PgpPublicKeyType.EdDSA:
            default:
                throw new NotSupportedException($"Public Key type {signatureInfo.PublicKeyType} not implemented yet");
        }
    }

    public override void Reset()
    {
        base.Reset();

        _stack.Clear();
        _stack.Push(Source);

        _reader = null;
        _position = 0;
    }

    internal static ReadOnlyMemory<byte> CreateSshFingerprint(CryptoAlgorithm sba, IReadOnlyList<BigInteger> keyInts)
    {
        ByteCollector bb = new(4096);

        var ints = keyInts.Select(x => x.ToCryptoValue(unsigned: false)).ToList();

        string alg;
        switch (sba)
        {
            case CryptoAlgorithm.Rsa:
                alg = "ssh-rsa";
                (ints[1], ints[0]) = (ints[0], ints[1]);
                break;
            case CryptoAlgorithm.Dsa:
                alg = "ssh-dss";
                break;
            case CryptoAlgorithm.Ed25519:
                alg = "ssh-ed25519";
                break;
            case CryptoAlgorithm.Ecdsa:
                string kv = CryptoExtensions.GetCurveName(keyInts[0]);

                if (kv.StartsWith("ECDSA_", StringComparison.OrdinalIgnoreCase))
#pragma warning disable CA1308 // Normalize strings to uppercase
                    kv = "nist" + kv.Substring(6).ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase
                alg = "ecdsa-sha2-" + kv.ToString();
                break;
            default:
                throw new NotSupportedException($"Unknown ssh fingerprint type {sba}");
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

    public override long? Position => _position;
}
