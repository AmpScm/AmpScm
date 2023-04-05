using System;
using System.Numerics;
using System.Security.Cryptography;

namespace AmpScm.Buckets.Cryptography.Algorithms;

internal enum ElgaEncryptionPadding
{
    None,
    Pkcs1
}

internal sealed class Elgamal : IDisposable
{
    private Elgamal()
    { }

    public static Elgamal Create()
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

    public byte[] Decrypt(BigInteger c1, BigInteger c2, ElgaEncryptionPadding padding)
    {
        var result = (BigInteger.ModPow(c1, X, P).ModInverse(P) * c2) % P;

        // Returned value should be 0x00 0x02 | <random-data> | 0 | message, but may start with '2' directly
        // because we convert to+from bigint
        var r = result.ToCryptoValue();

        switch (padding)
        {
            case ElgaEncryptionPadding.None:
                return r;
            case ElgaEncryptionPadding.Pkcs1:
                Span<byte> rs = r;

                int start;
                if (rs[0] == 2)
                    start = 2;
                else if (rs[0] == 0 && rs[1] == 2)
                    start = 3;
                else
                    throw new CryptographicException($"{nameof(Elgamal)} Decrypt failed");

                var firstZero = rs.Slice(start).IndexOf((byte)0);

                return rs.Slice(firstZero + start + 1).ToArray();
            default:
                throw new ArgumentOutOfRangeException(nameof(padding), padding, message: null);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
