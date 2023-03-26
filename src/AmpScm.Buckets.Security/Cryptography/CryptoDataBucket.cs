using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Cryptography;
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

                using HashAlgorithm ha = s2k.HashAlgorithm switch
                {
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
                    OpenPgpHashAlgorithm.SHA1 => SHA1.Create(),
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
                    OpenPgpHashAlgorithm.SHA256 => SHA256.Create(),
                    _ => throw new InvalidOperationException()
                };

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

        private protected static async ValueTask CreateHash(Bucket sourceData, Action<byte[]> created, OpenPgpHashAlgorithm hashAlgorithm)
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

        private protected static HashAlgorithmName GetDotNetHashAlgorithmName(OpenPgpHashAlgorithm hashAlgorithm)
        => hashAlgorithm switch
        {
            OpenPgpHashAlgorithm.SHA256 => HashAlgorithmName.SHA256,
            OpenPgpHashAlgorithm.SHA512 => HashAlgorithmName.SHA512,
            OpenPgpHashAlgorithm.SHA384 => HashAlgorithmName.SHA384,
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

        internal static async ValueTask<ReadOnlyMemory<byte>?> ReadPgpMultiPrecisionInteger(Bucket sourceData)
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
    }
}
