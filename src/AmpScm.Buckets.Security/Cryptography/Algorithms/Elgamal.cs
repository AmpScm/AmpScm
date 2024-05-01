using System.Numerics;
using System.Security.Cryptography;

namespace AmpScm.Buckets.Cryptography.Algorithms;

internal enum ElgamalEncryptionPadding
{
    None,
    Pkcs1
}

internal sealed class Elgamal : AsymmetricAlgorithm
{
    private Elgamal()
    { }

    public static new Elgamal Create()
    {
        return new Elgamal();
    }

    /// <summary>
    /// Prime p
    /// </summary>
    public BigInteger P { get; set; }

    /// <summary>
    /// Group generator g;
    /// </summary>
    public BigInteger G { get; set; }

    /// <summary>
    /// Public key value y (= g**x mod p where x is secret).
    /// </summary>
    public BigInteger Y { get; set; }

    /// <summary>
    /// Elgamal secret exponent x.
    /// </summary>
    public BigInteger X { get; set; }

    public byte[] Decrypt(BigInteger c1, BigInteger c2, ElgamalEncryptionPadding padding)
    {
        // The
        var result = (BigInteger.ModPow(c1, X, P).ModInverse(P) * c2) % P;

        // Returned value should be 0x00 0x02 | <random-data> | 0 | message, but may start with '2' directly
        // because we convert to+from bigint
        var r = result.ToCryptoValue();

        switch (padding)
        {
            case ElgamalEncryptionPadding.None:
                return r;
            case ElgamalEncryptionPadding.Pkcs1:
                Span<byte> rs = r;

                if (rs[0] == 2)
                    rs = rs.Slice(1);
                else if (rs[0] == 0 && rs[1] == 2)
                    rs = rs.Slice(2);
                else
                    throw new CryptographicException($"{nameof(Elgamal)} Decrypt failed");

                //rs = rs.Slice(8); // Minimal 8 bytes of garbage != 0

                var firstZero = rs.IndexOf((byte)0);

                return rs.Slice(firstZero + 1).ToArray();
            default:
                throw new ArgumentOutOfRangeException(nameof(padding), padding, message: null);
        }
    }
}
