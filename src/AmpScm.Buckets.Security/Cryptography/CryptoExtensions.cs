using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Cryptography
{
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
            return value.ToByteArray(true, true);
        }

        internal static BigInteger ToBigInteger(this byte[] value)
        {
            return ToBigInteger(value.AsMemory());
        }

        internal static BigInteger ToBigInteger(this ReadOnlyMemory<byte> value)
        {
#if NETCOREAPP
            return new BigInteger(value.Span, true, true);
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

        static BigInteger ModInverse(BigInteger a, BigInteger n)
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
                t = t + n;

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
                p.InverseQ = ModInverse(Q, P).ToCryptoValue().AlignUp();
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

            var v = ints[0].ToCryptoValue();
            var v2 = ints[1].ToCryptoValue();
            var v3 = ints[2].ToCryptoValue();

            var curveOid = ParseOid(v);
            if (v2[0] == 0x04)
            {
                int n = (v2.Length - 1) / 2;

                v3 = v2.Skip(n + 1).ToArray();
                v2 = v2.Skip(1).Take(n).ToArray();
            }

            var p = new ECParameters()
            {
                // The name is stored as integer... Nice :(
                Curve = ECCurve.CreateFromValue(curveOid),

                Q = new ECPoint
                {
                    X = v2.AlignUp(),
                    Y = v3.AlignUp(),
                },
            };

            if (ints.Count == 4)
                p.D = ints[3].ToCryptoValue().AlignUp();

            if (p.Q.X.Length != p.Q.Y.Length)
            {
                int len = Math.Max(p.Q.X.Length, p.Q.Y.Length);

                p.Q = new ECPoint
                {
                    X = MakeLength(p.Q.X, len),
                    Y = MakeLength(p.Q.Y, len),
                };
            }

            ecdh.ImportParameters(p);
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
            else if (ints.Count is not 3)
                throw new ArgumentOutOfRangeException(nameof(ints), ints.Count, message: null);

            string curveName = Encoding.ASCII.GetString(ints[0].ToCryptoValue());

            // SignaturePublicKey must be concattenation of 2 values with same number of bytes

            var p = new ECParameters()
            {
                // The name is stored as integer... Nice :(
                Curve = ECCurve.CreateFromFriendlyName(curveName),

                Q = new ECPoint
                {
                    X = ints[1].ToCryptoValue().AlignUp(),
                    Y = ints[2].ToCryptoValue().AlignUp(),
                },
            };

            if (p.Q.X.Length != p.Q.Y.Length)
            {
                int len = Math.Max(p.Q.X.Length, p.Q.Y.Length);

                p.Q = new ECPoint
                {
                    X = MakeLength(p.Q.X, len),
                    Y = MakeLength(p.Q.Y, len),
                };
            }

            eCDsa.ImportParameters(p);

        }

        private static byte[] MakeLength(byte[] array, int len)
        {
            if (array.Length == len)
                return array;
            else
                return Enumerable.Range(0, len - array.Length).Select(_ => (byte)0).Concat(array).ToArray();
        }

        internal static SymmetricAlgorithm ApplyModeShim(this SymmetricAlgorithm algorithm)
        {
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
            if (algorithm.Mode == CipherMode.CFB)
            {
                return new CfbMapper(algorithm);
            }
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts

            return algorithm;
        }
    }
}
