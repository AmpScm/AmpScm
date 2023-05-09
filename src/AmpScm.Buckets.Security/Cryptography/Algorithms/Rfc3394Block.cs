/* Originally based on https://www.codeproject.com/articles/30590/rfc3394-key-wrapping-algorithm-in-c
 *
 * RFC3394 Key Wrapping Algorithm
 * Written by Jay Miller
 *
 * This code is hereby released into the public domain, This applies
 * worldwide.
 */

using System;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Cryptography;

internal partial class Rfc3394Algorithm
{
    /// <summary>
    /// A <b>Block</b> contains exactly 64 bits of data.  This class
    /// provides several handy block-level operations.
    /// </summary>
    struct Rfc3394Block
    {
        public Rfc3394Block(ReadOnlyMemory<byte> bytes)
        {
            if (bytes.Length != 8)
                throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length, message: null);

            Bytes = bytes;
        }

        // Gets the contents of the current Block.
        public ReadOnlyMemory<byte> Bytes { get; }

        // Concatenates the current Block with the specified Block.
        public byte[] Concat(Rfc3394Block right)
        {
            byte[] output = new byte[16];

            Bytes.Span.CopyTo(output);
            right.Bytes.Span.CopyTo(output.AsSpan(8));

            return output;
        }

        // Converts an array of bytes to an array of Blocks.
        public static Rfc3394Block[] BytesToBlocks(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length % 8 != 0)
                throw new ArgumentOutOfRangeException(nameof(bytes), bytes, message: null);

            Rfc3394Block[] blocks = new Rfc3394Block[bytes.Length / 8];

            for (int i = 0; i < bytes.Length; i += 8)
                blocks[i / 8] = new Rfc3394Block(bytes.AsMemory(i, 8));

            return blocks;
        }

        // Converts an array of Blocks to an arry of bytes.
        public static byte[] BlocksToBytes(Rfc3394Block[] blocks)
        {
            if (blocks == null)
                throw new ArgumentNullException(nameof(blocks));

            byte[] bytes = new byte[blocks.Length * 8];

            for (int i = 0; i < blocks.Length; i++)
                blocks[i].Bytes.CopyTo(bytes.AsMemory(i * 8));

            return bytes;
        }

        // XOR operator against a 64-bit value.
        public static Rfc3394Block operator ^(Rfc3394Block left, long right)
        {
            return Xor(left, right);
        }

        // XORs a block with a 64-bit value.
        static Rfc3394Block Xor(Rfc3394Block left, long right)
        {
            var bb = left.Bytes.ToArray();
            long temp = NetBitConverter.ToInt64(bb, 0);

            return new Rfc3394Block(NetBitConverter.GetBytes(temp ^ right));
        }

        // Swaps the byte positions in the specified array.
        internal static void ReverseBytes(byte[] bytes)
        {
            if (bytes is null)
                throw new ArgumentNullException(nameof(bytes));
#if NETCOREAPP
            Array.Reverse(bytes);
#else
            for (int i = 0; i < bytes.Length / 2; i++)
            {
               byte temp = bytes[i];
               bytes[i] = bytes[(bytes.Length - 1) - i];
               bytes[(bytes.Length - 1) - i] = temp;
            }
#endif
        }
    }
}
