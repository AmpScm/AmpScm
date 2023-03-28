using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Cryptography
{
    public static partial class CryptoExtensions
    {

        internal static IReadOnlyList<BigInteger> ToBigInts(this IReadOnlyList<ReadOnlyMemory<byte>> list)
        {
            if (list is null)
                throw new ArgumentNullException(nameof(list));

            return list.Select(x => x.ToBigInteger()).ToList();
        }

        internal static byte[] ToCryptoValue(this BigInteger value)
        {
#if NETCOREAPP
            return value.ToByteArray(true, true);
#else
            return value.ToByteArray().Reverse().ToArray();
#endif
        }

        internal static BigInteger ToBigInteger(this ReadOnlyMemory<byte> value)
        {
#if NETCOREAPP
            return new BigInteger(value.ToArray(), true, true);
#else
            return new BigInteger(value.ToArray().Reverse().ToArray());
#endif
        }

        internal static BigInteger ToBigInteger(this byte[] value)
        {
            return ToBigInteger(new Memory<byte>(value));
        }

        internal static BigInteger ToBigInteger(this IEnumerable<byte> value)
        {
            return ToBigInteger(value.ToArray());
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
                Modulus = ints[0].ToCryptoValue(),
                Exponent = ints[1].ToCryptoValue(),
            };

            if (ints.Count > 2)
            {
                BigInteger D = ints[2];
                BigInteger P = ints[3];
                BigInteger Q = ints[4];


                BigInteger DP = D % (P - 1);
                BigInteger DQ = D % (Q - 1);

                p.D = D.ToCryptoValue();
                p.P = P.ToCryptoValue();
                p.Q = Q.ToCryptoValue();
                p.InverseQ = ints[5].ToCryptoValue();
                p.DP = DP.ToCryptoValue();
                p.DQ = DQ.ToCryptoValue();
            }

            rsa.ImportParameters(p);
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
                P = ints[0].ToCryptoValue(),
                Q = ints[1].ToCryptoValue(),
                G = ints[2].ToCryptoValue(),
                Y = ints[3].ToCryptoValue()
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
                    X = ints[1].ToCryptoValue(),
                    Y = ints[2].ToCryptoValue(),
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
    }
}
