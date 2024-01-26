﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Cryptography.Algorithms;

namespace AmpScm.Buckets.Cryptography;

public static partial class CryptoExtensions
{

#if NETFRAMEWORK
    internal static byte[] ToByteArray(this BigInteger value, bool isUnsigned = false, bool isBigEndian = false)
    {
        var r = value.ToByteArray();

        if (isBigEndian)
        {
            Array.Reverse(r);
        }

        if (isUnsigned && r[0] == 0)
        {
            byte[] r2 = new byte[r.Length - 1];

            r.AsSpan(1).CopyTo(r2);
            r = r2;
        }

        return r;
    }
#endif

    internal static byte[] ToCryptoValue(this BigInteger value)
    {
        return value.ToByteArray(isUnsigned: true, isBigEndian: true);
    }

    internal static byte[] ToCryptoValue(this BigInteger value, bool unsigned)
    {
        return value.ToByteArray(unsigned, isBigEndian: true);
    }

    internal static BigInteger ToBigInteger(this byte[] value)
    {
        return ToBigInteger(value.AsMemory());
    }

    internal static BigInteger ToBigInteger(this ReadOnlyMemory<byte> value)
    {
#if NETCOREAPP
        return new BigInteger(value.Span, isUnsigned: true, isBigEndian: true);
#else
        byte[] v;
        if ((value.Span[0] & 0x80) != 0)
        {
            v = new byte[value.Length + 1];
            value.Span.CopyTo(v.AsSpan(1));
        }
        else
            v = value.ToArray();

        Array.Reverse(v);

        return new BigInteger(v);
#endif
    }

    internal static BigInteger ToBigInteger(this IEnumerable<byte> value)
    {
        return ToBigInteger(value.ToArray());
    }

    internal static byte[] AlignUp(this byte[] bytes, int mask = 4)
    {
        if ((bytes.Length & (mask - 1)) == 0)
            return bytes;
        else
        {
            var newLen = bytes.Length - bytes.Length % mask + mask;

            return bytes.PadLeft(newLen);
        }
    }

    internal static byte[] PadRight(this byte[] bytes, int length)
    {
        if (bytes.Length >= length)
            return bytes;
        else
        {
            byte[] newBytes = new byte[length];

            bytes.AsSpan().CopyTo(newBytes);

            return newBytes;
        }
    }

    internal static byte[] PadLeft(this byte[] bytes, int length)
    {
        if (bytes.Length >= length)
            return bytes;
        else
        {
            byte[] newBytes = new byte[length];

            bytes.AsSpan().CopyTo(newBytes.AsSpan(length - bytes.Length));

            return newBytes;
        }
    }

    internal static BigInteger ModInverse(this BigInteger a, BigInteger n)
    {
        BigInteger t = 0;
        BigInteger newt = 1;
        BigInteger r = n;
        BigInteger newr = a;

        if (a < 0)
            throw new ArgumentOutOfRangeException(nameof(a), a, message: default);
        if (n < 0)
            throw new ArgumentOutOfRangeException(nameof(n), n, message: default);

        while (newr != 0)
        {
            var quotient = r / newr;

            (t, newt) = (newt, t - quotient * newt);
            (r, newr) = (newr, r - quotient * newr);
        }

        if (r > 1)
            throw new ArgumentOutOfRangeException(nameof(a), a, "not convertable");

        if (t < 0)
            t += n;

        return t;
    }

    internal static void ImportParametersFromCryptoInts(this RSA rsa, IReadOnlyList<BigInteger> ints)
    {
        if (rsa is null)
            throw new ArgumentNullException(nameof(rsa));
        else if (ints is null)
            throw new ArgumentNullException(nameof(ints));
        else if (ints.Count is not 2 and not 6)
            throw new ArgumentOutOfRangeException(nameof(ints), ints.Count, message: null);

        var p = new RSAParameters()
        {
            Modulus = ints[0].ToCryptoValue().AlignUp(),
            Exponent = ints[1].ToCryptoValue().AlignUp(),
        };

        rsa.KeySize = p.Modulus.Length * 8;

        if (ints.Count > 2)
        {
            BigInteger D = ints[2];
            BigInteger P = ints[3];
            BigInteger Q = ints[4];
            // ints[5] is ignored. This is NOT InverseQ

            BigInteger DP = D % (P - 1);
            BigInteger DQ = D % (Q - 1);

            p.D = D.ToCryptoValue().AlignUp();
            p.P = P.ToCryptoValue().AlignUp();
            p.Q = Q.ToCryptoValue().AlignUp();
            p.InverseQ = Q.ModInverse(P).ToCryptoValue().AlignUp();
            p.DP = DP.ToCryptoValue().AlignUp();
            p.DQ = DQ.ToCryptoValue().AlignUp();
        }

        rsa.ImportParameters(p);
    }

    internal static void ImportParametersFromCryptoInts(this ECDiffieHellman ecdh, IReadOnlyList<BigInteger> ints)
    {
        if (ecdh is null)
            throw new ArgumentNullException(nameof(ecdh));
        else if (ints is null)
            throw new ArgumentNullException(nameof(ints));
        else if (ints.Count is not 2 and not 4)
            throw new ArgumentOutOfRangeException(nameof(ints), ints.Count, message: null);

        var oidValue = ints[0].ToCryptoValue();
        var x = ints[1].ToCryptoValue();
        byte[] y;

        var curveOid = ParseOid(oidValue);
        if (x[0] == 0x04)
        {
            int n = (x.Length - 1) / 2;

            y = x.Skip(n + 1).ToArray();
            x = x.Skip(1).Take(n).ToArray();
        }
        else // 0x40 -> native format, 0x41 -> Only X, 0x42 -> Only Y
            throw new NotImplementedException($"Unexpected ECDH keyformat with first byte {x[0]}");

        var p = new ECParameters()
        {
            // The name is stored as integer... Nice :(
            Curve = ECCurve.CreateFromValue(curveOid),

            Q = new ECPoint
            {
                X = x,
                Y = y,
            },
        };

        //ECParameters.De

        if (ints.Count == 4)
            p.D = ints[3].ToCryptoValue().AlignUp();

        ecdh.ImportParameters(p);
    }

    internal static void ImportParametersFromCryptoInts(this Elgamal elgamal, IReadOnlyList<BigInteger> ints)
    {
        elgamal.P = ints[0]; // Elgamal prime p
        elgamal.G = ints[1]; // Elgamal group generator g;
        elgamal.Y = ints[2]; // Elgamal public key value y (= g**x mod p where x is secret).
        elgamal.X = ints[3]; // Elgamal secret exponent x.
    }

    internal static void ImportParametersFromCryptoInts(this Curve25519 curve25519, IReadOnlyList<BigInteger> ints)
    {
        if (curve25519 is null)
            throw new ArgumentNullException(nameof(curve25519));
        else if (ints is null)
            throw new ArgumentNullException(nameof(ints));
        else if (ints.Count is not 2 and not 4)
            throw new ArgumentOutOfRangeException(nameof(ints), ints.Count, message: null);

        // ints[0] is oid
        var v = ints[1].ToCryptoValue();

        if (v[0] == 0x40)
        {
            curve25519.PublicKey = v.Skip(1).ToArray();
        }
        else
            throw new NotImplementedException($"Unexpected Curve25519 keyformat with first byte {v[0]}");

        // ints[2] is kdf
        if (ints.Count == 4)
        {
            curve25519.PrivateKey = ints[3].ToCryptoValue().AlignUp();


            //var pk = Curve25519.GetPublicKey(curve25519.PrivateKey);
            //
            //Debug.Assert(pk.AsSpan().SequenceEqual(curve25519.PublicKey));
        }
    }

    internal static ECDiffieHellmanPublicKey CreatePublicKey(this ECDiffieHellman ecdh, BigInteger point)
    {
        using ECDiffieHellman ecdh2 = ECDiffieHellman.Create();

        var p = ecdh.ExportParameters(includePrivateParameters: false);

        var v = point.ToCryptoValue();
        if (v[0] != 0x04)
            throw new InvalidOperationException();

        int n = (v.Length - 1) / 2;
        var v1 = v.Skip(1).Take(n).ToArray();
        var v2 = v.Skip(1 + n).ToArray();

        p.Q = new ECPoint { X = v1, Y = v2 };

        ecdh2.ImportParameters(p);

        return ecdh2.PublicKey;
    }

    internal static byte[] CreatePublicKey(this Curve25519 curve25591, BigInteger value)
    {
        var v = value.ToCryptoValue();

        if (v[0] == 0x40)
        {
            return v.Skip(1).ToArray();
        }

        throw new NotImplementedException($"Unexpected Curve25519 keyformat with first byte {v[0]}");
    }

    internal static byte[] DeriveKeyFromHash(this Curve25519 curve25519, byte[] publicKey, HashAlgorithm hashAlgorithm, byte[]? secretPrepend = null, byte[]? secretAppend = null)
    {
        var pk = curve25519.PrivateKey!;

        //Curve25519.al

        var sharedSecret = Curve25519.GetSharedSecret(pk, publicKey);

        if (secretPrepend != null)
            hashAlgorithm.TransformBlock(secretPrepend, 0, secretPrepend.Length, outputBuffer: null, 0);

        hashAlgorithm.TransformBlock(sharedSecret, 0, sharedSecret.Length, outputBuffer: null, 0);

        if (secretAppend != null)
            hashAlgorithm.TransformBlock(secretAppend, 0, secretAppend.Length, outputBuffer: null, 0);


        hashAlgorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

        return hashAlgorithm.Hash!;
    }

    private static string ParseOid(byte[] v)
    {
        StringBuilder sb = new StringBuilder();

        int v0 = v[0] / 40;
        int v1 = v[0] % 40;

        sb.Append(v0);
        sb.Append('.');
        sb.Append(v1);

        int i = 1;
        while (i < v.Length)
        {
            sb.Append('.');
            if (v[i] < 128)
            {
                sb.Append((int)v[i]);

                i++;
            }
            else
            {
                long lv = (v[i] & 0x7F);
                i++;

                while (v[i] > 0x7F)
                {
                    lv = (lv << 7) | (long)(v[i] & 0x7F);
                    i++;
                }

                lv = (lv << 7) | (long)(v[i] & 0x7F);
                i++;

                sb.Append(lv);
            }
        }

        return sb.ToString();
    }

    internal static void ImportParametersFromCryptoInts(this DSA dsa, IReadOnlyList<BigInteger> ints)
    {
        if (dsa is null)
            throw new ArgumentNullException(nameof(dsa));
        else if (ints is null)
            throw new ArgumentNullException(nameof(ints));
        else if (ints.Count is not 4)
            throw new ArgumentOutOfRangeException(nameof(ints), ints.Count, message: null);

        var p = new DSAParameters()
        {
            P = ints[0].ToCryptoValue().AlignUp(),
            Q = ints[1].ToCryptoValue().AlignUp(),
            G = ints[2].ToCryptoValue().AlignUp(),
            Y = ints[3].ToCryptoValue().AlignUp()
        };

        dsa.ImportParameters(p);
    }

    internal static void ImportParametersFromCryptoInts(this ECDsa eCDsa, IReadOnlyList<BigInteger> ints)
    {
        if (eCDsa is null)
            throw new ArgumentNullException(nameof(eCDsa));
        else if (ints is null)
            throw new ArgumentNullException(nameof(ints));
        else if (ints.Count is not 2)
            throw new ArgumentOutOfRangeException(nameof(ints), ints.Count, message: null);

        var curveValue = ints[0].ToCryptoValue();


        byte[] v1 = ints[1].ToCryptoValue();
        byte[] v2;

        if (v1[0] == 0x04)
        {
            int n = (v1.Length - 1) / 2;

            v2 = v1.Skip(n + 1).ToArray();
            v1 = v1.Skip(1).Take(n).ToArray();
        }
        else
            throw new NotImplementedException($"Unexpected ECDSA keyformat with first byte {v1[0]}");


        var oidString = ParseOid(curveValue);

        var oid = new Oid(oidString);

        var p = new ECParameters()
        {
            // The name is stored as integer... Nice :(
            Curve = ECCurve.CreateFromOid(oid),

            Q = new ECPoint
            {
                X = v1,
                Y = v2,
            },
        };

        eCDsa.ImportParameters(p);
    }

    internal static SymmetricAlgorithm ApplyModeShim(this SymmetricAlgorithm algorithm)
    {
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
        if (algorithm.Mode == CipherMode.CFB
#if NET6_0_OR_GREATER
            && !OperatingSystem.IsWindows()
#endif
            )
        {
            return new CfbMapper(algorithm);
        }
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts

        return algorithm;
    }

    internal static string GetCurveName(BigInteger bigInteger)
    {
        var bytes = bigInteger.ToCryptoValue();
        var oid = ParseOid(bytes);

        var c = new Oid(oid);

        return c.FriendlyName ?? throw new ArgumentOutOfRangeException(nameof(bigInteger), bigInteger, message: null);
    }

    internal static HashAlgorithmName GetHashAlgorithmName(this PgpHashAlgorithm hashAlgorithm)
    {
        return hashAlgorithm switch
        {
            PgpHashAlgorithm.SHA256 => HashAlgorithmName.SHA256,
            PgpHashAlgorithm.SHA512 => HashAlgorithmName.SHA512,
            PgpHashAlgorithm.SHA384 => HashAlgorithmName.SHA384,
            PgpHashAlgorithm.SHA1 => HashAlgorithmName.SHA1,
            PgpHashAlgorithm.MD5 => HashAlgorithmName.MD5,
            _ => throw new NotImplementedException($"PGP scheme {hashAlgorithm} not mapped yet.")
        };
    }

    internal static int GetKeySize(this PgpSymmetricAlgorithm cipherAlgorithm)
    {
        return cipherAlgorithm switch
        {
            PgpSymmetricAlgorithm.Aes => 128,
            PgpSymmetricAlgorithm.Aes192 => 192,
            PgpSymmetricAlgorithm.Aes256 => 256,
            PgpSymmetricAlgorithm.TripleDes => 192,
            PgpSymmetricAlgorithm.Blowfish128 => 128,
            _ => throw new NotImplementedException($"Keysize for cipher {cipherAlgorithm} not implemented yet.")
        };
    }

    internal static int GetKeyBytes(this PgpSymmetricAlgorithm cipherAlgorithm)
    {
        return cipherAlgorithm.GetKeySize() / 8;
    }

    internal static int GetBlockBytes(this PgpSymmetricAlgorithm cipherAlgorithm)
    {
        return cipherAlgorithm switch
        {
            PgpSymmetricAlgorithm.Aes or PgpSymmetricAlgorithm.Aes192 or PgpSymmetricAlgorithm.Aes256 => 16,
            _ => throw new NotImplementedException($"Blocksize for cipher {cipherAlgorithm} not implemented yet.")
        };
    }
}
