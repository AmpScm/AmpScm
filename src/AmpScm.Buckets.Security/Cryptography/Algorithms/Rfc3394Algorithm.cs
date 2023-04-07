using System;
using System.Security.Cryptography;

namespace AmpScm.Buckets.Cryptography.Algorithms;

// From https://github.com/digitallyrefreshed/RFC3394.net, MIT Licensed

/// <summary>
/// Implements AES key wrapping according to RFC3394
/// </summary>
internal sealed class Rfc3394Algorithm : IDisposable
{
    private static readonly byte[] initialValue = new byte[] { 0xa6, 0xa6, 0xa6, 0xa6, 0xa6, 0xa6, 0xa6, 0xa6 };
    private readonly Aes aes;

    private static readonly int[] allowedLengths = new int[] { 128, 192, 256 };

    /// <summary>
    /// Instantiates a new KeyWrapAlgorithm object
    /// </summary>
    public Rfc3394Algorithm()
    {
        // set up AES for propper encryption/decryption
        // padding must be none, mode must be ECB, IV must be 0
        aes = Aes.Create();
        aes.Padding = PaddingMode.None;
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
        aes.Mode = CipherMode.ECB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
        aes.IV = new byte[aes.BlockSize / 8];
    }

    public void Dispose()
    {
        if (aes != null)
        {
            aes.Dispose();
        }
    }

    /// <summary>
    /// Wraps a plain text key with a key encryption key (KEK)
    /// </summary>
    /// <param name="kek">The key encryption key to use</param>
    /// <param name="plainKey">The plain text key to wrap</param>
    /// <returns>The wrapped key</returns>
    /// <exception cref="ArgumentNullException"><c>kek</c> or <c>plainKey</c> are null</exception>
    /// <exception cref="ArgumentException"><c>kek</c> or <c>plainKey</c> have an invalid length</exception>
    public byte[] Wrap(byte[] kek, byte[] plainKey)
    {
        if (kek == null)
        {
            throw new ArgumentNullException(nameof(kek));
        }
        if (plainKey == null)
        {
            throw new ArgumentNullException(nameof(plainKey));
        }

        bool kekLengthValid = false;
        for (int i = 0; i < allowedLengths.Length; i++)
        {
            if (allowedLengths[i] == kek.Length * 8)
            {
                kekLengthValid = true;
                break;
            }
        }

        if (!kekLengthValid)
        {
            throw new ArgumentException($"Length of {nameof(kek)} must be one of {string.Join(", ", allowedLengths)} bits", nameof(kek));
        }

        if (plainKey.Length % 8 != 0)
        {
            throw new ArgumentException($"Length of {nameof(plainKey)} must be a multiple of 64 bits", nameof(plainKey));
        }

        if (plainKey.Length > kek.Length)
        {
            throw new ArgumentException($"Length of {nameof(plainKey)} must be less than or equal to {nameof(kek)}", nameof(plainKey));
        }

        // setup
        int blockCount = plainKey.Length / 8;

        byte[] integrityCheck = new byte[8];
        byte[] registers = new byte[blockCount * 8];
        byte[] cipherText = new byte[(blockCount + 1) * 8];

        // initialize
        Array.Copy(initialValue, integrityCheck, integrityCheck.Length);
        Array.Copy(plainKey, registers, plainKey.Length);

        // AES
        aes.Key = kek;

        // wrap plain key in 6 * blockCount steps
        // this is intentionally not using a CryptoStream for improved performance
        using (ICryptoTransform transform = aes.CreateEncryptor())
        {
            for (int j = 0; j < 6; j++)
            {
                for (int i = 0; i < blockCount; i++)
                {
                    byte t = (byte)((blockCount * j) + i + 1);
                    int blockSize = (aes.BlockSize / 8);
                    byte[] inputBlock = new byte[blockSize];
                    byte[] outputBlock;

                    // prepare the block data for encryption
                    Array.Copy(integrityCheck, inputBlock, integrityCheck.Length);
                    Array.Copy(registers, i * 8, inputBlock, 8, blockSize - integrityCheck.Length);

                    // encrypt
                    outputBlock = transform.TransformFinalBlock(inputBlock, 0, inputBlock.Length);

                    // prepare for next round
                    byte[] msb = Rfc3394Algorithm.MSB(8, outputBlock);
                    byte[] lsb = Rfc3394Algorithm.LSB(8, outputBlock);

                    Array.Copy(msb, integrityCheck, msb.Length);

                    // XOR with the step counter
                    integrityCheck[7] ^= t;

                    Array.Copy(lsb, 0, registers, i * 8, lsb.Length);
                }
            }
        }

        // prepare output
        Array.Copy(integrityCheck, cipherText, integrityCheck.Length);
        Array.Copy(registers, 0, cipherText, integrityCheck.Length, registers.Length);

        return cipherText;
    }

    /// <summary>
    /// Unwraps an encrypted key with a key encryption key (KEK)
    /// </summary>
    /// <param name="kek">The key encryption key to use</param>
    /// <param name="wrappedKey">The wrapped key</param>
    /// <returns>The plain text key</returns>
    /// <exception cref="ArgumentNullException"><c>kek</c> or <c>plainKey</c> are null</exception>
    /// <exception cref="ArgumentException"><c>kek</c> or <c>plainKey</c> have an invalid length</exception>
    /// <exception cref="CryptographicException">The decrypted key failed the integrity check</exception>
    public byte[] Unwrap(byte[] kek, byte[] wrappedKey)
    {
        if (kek == null)
        {
            throw new ArgumentNullException(nameof(kek));
        }
        if (wrappedKey == null)
        {
            throw new ArgumentNullException(nameof(wrappedKey));
        }

        bool kekLengthValid = false;

        for (int i = 0; i < allowedLengths.Length; i++)
        {
            if (allowedLengths[i] == kek.Length * 8)
            {
                kekLengthValid = true;
                break;
            }
        }

        if (!kekLengthValid)
        {
            throw new ArgumentOutOfRangeException(nameof(kek), kek.Length, $"Length of {nameof(kek)} must be one of {string.Join(", ", allowedLengths)} bits");
        }

        if (wrappedKey.Length % 8 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(wrappedKey), wrappedKey.Length, $"Length of {nameof(wrappedKey)} must be a multiple of 64 bits");
        }

        if (wrappedKey.Length > (kek.Length + 8))
        {
            throw new ArgumentOutOfRangeException(nameof(wrappedKey), wrappedKey.Length, $"Length of {nameof(wrappedKey)} is invalid for this key size");
        }

        // setup
        int blockCount = (wrappedKey.Length / 8) - 1; // the wrapped key is one block larger than the plain text key; therefore we subtract 1

        byte[] integrityCheck = new byte[8];
        byte[] registers = new byte[blockCount * 8];
        byte[] plainText = new byte[blockCount * 8];

        // initialize
        Array.Copy(wrappedKey, 0, integrityCheck, 0, integrityCheck.Length);
        Array.Copy(wrappedKey, integrityCheck.Length, registers, 0, registers.Length);

        // set AES key (KEK)
        aes.Key = kek;

        // unwrap wrapped key in 6 * blockCount steps
        // this is intentionally not using a CryptoStream for improved performance
        using (ICryptoTransform transform = aes.CreateDecryptor())
        {
            for (int j = 5; j >= 0; j--)
            {
                for (int i = blockCount - 1; i >= 0; i--)
                {
                    byte t = (byte)((blockCount * j) + i + 1);
                    int blockSize = (aes.BlockSize / 8);
                    byte[] inputBlock = new byte[blockSize];
                    byte[] outputBlock;

                    // prepare the input for decryption
                    Array.Copy(integrityCheck, 0, inputBlock, 0, integrityCheck.Length);
                    Array.Copy(registers, i * 8, inputBlock, integrityCheck.Length, blockSize - integrityCheck.Length);

                    // XOR with the step counter
                    inputBlock[7] ^= t;

                    // decrypt
                    outputBlock = transform.TransformFinalBlock(inputBlock, 0, inputBlock.Length);

                    // prepare for next round
                    byte[] msb = Rfc3394Algorithm.MSB(8, outputBlock);
                    byte[] lsb = Rfc3394Algorithm.LSB(8, outputBlock);

                    Array.Copy(msb, 0, integrityCheck, 0, msb.Length);
                    Array.Copy(lsb, 0, registers, i * 8, lsb.Length);
                }
            }
        }

        // verify integrity
        for (int i = 0; i < 8; i++)
        {
            if (integrityCheck[i] != initialValue[i])
            {
                throw new CryptographicException("Integrity check failed");
            }
        }

        // prepare output
        Array.Copy(registers, 0, plainText, 0, registers.Length);

        return plainText;
    }

    /// <summary>
    /// Returns the most significant bytes of a byte array
    /// </summary>
    /// <param name="size">The number of bytes to return</param>
    /// <param name="buffer">The byte array</param>
    /// <returns>A byte array containing the specified most significant bytes</returns>
    private static byte[] MSB(int size, byte[] buffer)
    {
        // this is in reverse byte order

        byte[] outputBuffer = new byte[size];

        for (int i = 0; i < size; i++)
        {
            outputBuffer[i] = buffer[i];
        }

        return outputBuffer;
    }

    /// <summary>
    /// Returns the least significant bytes of a byte array
    /// </summary>
    /// <param name="size">The number of bytes to return</param>
    /// <param name="buffer">The byte array</param>
    /// <returns>A byte array containing the specified least significant bytes</returns>
    private static byte[] LSB(int size, byte[] buffer)
    {
        // this is in reverse byte order

        byte[] outputBuffer = new byte[size];

        for (int i = 0; i < size; i++)
        {
            outputBuffer[i] = buffer[buffer.Length - size + i];
        }

        return outputBuffer;
    }
}

