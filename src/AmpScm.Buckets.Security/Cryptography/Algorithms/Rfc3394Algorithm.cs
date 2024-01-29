/* Originally based on https://www.codeproject.com/articles/30590/rfc3394-key-wrapping-algorithm-in-c
 *
 * RFC3394 Key Wrapping Algorithm
 * Written by Jay Miller
 *
 * This code is hereby released into the public domain, This applies
 * worldwide.
 */

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace AmpScm.Buckets.Cryptography;

/// <summary>
/// An implementation of the RFC3394 key-wrapping algorithm.
/// </summary>
internal sealed partial class Rfc3394Algorithm : IDisposable
{
    private readonly ReadOnlyMemory<byte> DefaultIV = new byte[] { 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6 };
    private byte[] _kek;

    /// <summary>
    /// Constructs a <see cref="Rfc3394Algorithm" /> object with the specified key-encryption key.
    /// </summary>
    /// <param name="kek">The key-encryption key to use for subsequent wrapping and unwrapping operations.  This must be a valid AES key.</param>
    /// <exception cref="ArgumentNullException"><c>kek</c> was a null reference.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><c>kek</c> must be a valid AES key, either 128, 192 or 256 bits long.</exception>
    public Rfc3394Algorithm(byte[] kek)
    {
        if (kek == null)
            throw new ArgumentNullException(nameof(kek));
        if (kek.Length is not 16 and not 24 and not 32)
            throw new ArgumentOutOfRangeException(nameof(kek), kek, message: null);

        _kek = kek;
    }

    /// <summary>
    /// Wrap key data.
    /// </summary>
    /// <param name="plaintext">The key data, two or more 8-byte blocks.</param>
    /// <returns>The encrypted, wrapped data.</returns>
    /// <exception cref="ArgumentNullException"><c>plaintext</c> was <b>null</b>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The plaintext contained fewer than 16 bytes.</exception>
    /// <exception cref="ArgumentException"><c>plaintext</c> was not made up of 64-bit blocks.</exception>
    public byte[] WrapKey(byte[] plaintext)
    {
        if (plaintext == null)
            throw new ArgumentNullException(nameof(plaintext));
        if (plaintext.Length < 16 || plaintext.Length % 8 != 0)
            throw new ArgumentOutOfRangeException(nameof(plaintext), plaintext, message: null);

        Rfc3394Block A = new Rfc3394Block(DefaultIV);
        Rfc3394Block[] R = Rfc3394Block.BytesToBlocks(plaintext);
        long n = R.Length;

        for (long j = 0; j < 6; j++)
        {
            for (long i = 0; i < n; i++)
            {
                long t = n * j + i + 1;  // add 1 because i is zero-based

                Rfc3394Block[] B = Encrypt(A.Concat(R[i]));

                A = B.First();
                R[i] = B.Last();

                A ^= t;
            }
        }

        Rfc3394Block[] C = new Rfc3394Block[n + 1];
        C[0] = A;
        for (long i = 1; i <= n; i++)
            C[i] = R[i - 1];

        return Rfc3394Block.BlocksToBytes(C);
    }

    /// <summary>
    /// Unwrap encrypted key data.
    /// </summary>
    /// <param name="ciphertext">The encrypted key data, two or more 8-byte blocks.</param>
    /// <returns>The original key data.</returns>
    /// <exception cref="ArgumentNullException"><c>ciphertext</c> was <b>null</b>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The ciphertext contained fewer than 16 bytes.</exception>
    /// <exception cref="ArgumentException"><c>ciphertext</c> was not made up of 64-bit blocks.</exception>
    /// <exception cref="CryptographicException">The decryption process failed an integrity check.</exception>
    public byte[] UnwrapKey(byte[] ciphertext)
    {
        if (!TryUnwrapKey(ciphertext, out var result))
        {
            throw new CryptographicException();
        }

        return result;
    }

    public bool TryUnwrapKey(byte[] ciphertext, [NotNullWhen(true)] out byte[]? result)
    {
        if (ciphertext == null)
            throw new ArgumentNullException(nameof(ciphertext));
        if (ciphertext.Length < 16 || ciphertext.Length % 8 != 0)
            throw new ArgumentOutOfRangeException(nameof(ciphertext), ciphertext, message: null);

        Rfc3394Block[] C = Rfc3394Block.BytesToBlocks(ciphertext);

        Rfc3394Block A = C[0];
        Rfc3394Block[] R = new Rfc3394Block[C.Length - 1];
        for (int i = 1; i < C.Length; i++)
            R[i - 1] = C[i];
        long n = R.Length;

        for (long j = 5; j >= 0; j--)
        {
            for (long i = n - 1; i >= 0; i--)
            {
                long t = n * j + i + 1;  // add 1 because i is zero-based

                A ^= t;

                Rfc3394Block[] B = Decrypt(A.Concat(R[i]));

                A = B.First();
                R[i] = B.Last();
            }
        }

        if (!DefaultIV.Span.SequenceEqual(A.Bytes.Span))
        {
            result = null;
            return false;
        }

        result = Rfc3394Block.BlocksToBytes(R);
        return true;
    }

    #region Helper methods

    /// <summary>
    /// Encrypts a block of plaintext with AES.
    /// </summary>
    /// <param name="plaintext">Plaintext to encrypt.</param>
    /// <returns><see cref="Rfc3394Block"/> containing the ciphertext bytes.</returns>
    private Rfc3394Block[] Encrypt(byte[] plaintext)
    {
        using Aes aes = Aes.Create();
        aes.Padding = PaddingMode.None;
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
        aes.Mode = CipherMode.ECB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
        aes.Key = _kek;

        if (plaintext == null)
            throw new ArgumentNullException(nameof(plaintext));
        if (plaintext.Length != aes.BlockSize / 8)
            throw new ArgumentOutOfRangeException(nameof(plaintext), plaintext, message: null);

        byte[] ciphertext = new byte[aes.BlockSize / 8];

        using (MemoryStream ms = new MemoryStream(plaintext))
        using (ICryptoTransform xf = aes.CreateEncryptor())
        using (CryptoStream cs = new CryptoStream(ms, xf,
              CryptoStreamMode.Read))
#pragma warning disable MA0045 // Do not use blocking calls in a sync method (need to make calling method async)
            _ = cs.Read(ciphertext, 0, aes.BlockSize / 8);
#pragma warning restore MA0045 // Do not use blocking calls in a sync method (need to make calling method async)

        return Rfc3394Block.BytesToBlocks(ciphertext);
    }

    /// <summary>
    /// Decrypts a block of ciphertext with AES.
    /// </summary>
    /// <param name="ciphertext">Ciphertext to decrypt.</param>
    /// <returns><see cref="Rfc3394Block"/> containing the plaintext bytes.</returns>
    private Rfc3394Block[] Decrypt(byte[] ciphertext)
    {
        using Aes aes = Aes.Create();
        aes.Padding = PaddingMode.None;
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
        aes.Mode = CipherMode.ECB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
        aes.Key = _kek;

        if (ciphertext == null)
            throw new ArgumentNullException(nameof(ciphertext));
        if (ciphertext.Length != aes.BlockSize / 8)
            throw new ArgumentOutOfRangeException(nameof(ciphertext), ciphertext, message: null);

        byte[] plaintext;

        using (MemoryStream ms = new MemoryStream())
        using (ICryptoTransform xf = aes.CreateDecryptor())
        using (CryptoStream cs = new CryptoStream(ms, xf,
              CryptoStreamMode.Write))
        {
#pragma warning disable MA0045 // Do not use blocking calls in a sync method (need to make calling method async)
            cs.Write(ciphertext, 0, aes.BlockSize / 8);
#pragma warning restore MA0045 // Do not use blocking calls in a sync method (need to make calling method async)
            plaintext = ms.ToArray();
        }

        return Rfc3394Block.BytesToBlocks(plaintext);
    }

    public void Dispose()
    {
    }

    #endregion
}
