using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    /// <summary>
    /// Like <see cref="BitConverter"/>, but then for values in network order
    /// </summary>
    public static class NetBitConverter
    {
        public static byte[] GetBytes(short value)
        {
            return BitConverter.GetBytes(ToNetwork(value));
        }

        [CLSCompliant(false)]
        public static byte[] GetBytes(ushort value)
        {
            return BitConverter.GetBytes(ToNetwork(value));
        }

        public static byte[] GetBytes(int value)
        {
            return BitConverter.GetBytes(ToNetwork(value));
        }

        [CLSCompliant(false)]
        public static byte[] GetBytes(uint value)
        {
            return BitConverter.GetBytes(ToNetwork(value));
        }

        public static byte[] GetBytes(long value)
        {
            return BitConverter.GetBytes(ToNetwork(value));
        }

        [CLSCompliant(false)]
        public static byte[] GetBytes(ulong value)
        {
            return BitConverter.GetBytes(ToNetwork(value));
        }

        public static short FromNetwork(short value)
        {
            if (BitConverter.IsLittleEndian)
            {
                ushort val = unchecked((ushort)value);
                value = unchecked((short)((val >> 8) | (val << 8)));
            }
            return value;
        }

        [CLSCompliant(false)]
        public static ushort FromNetwork(ushort value)
        {
            if (BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }
            return value;
        }

        public static int FromNetwork(int value)
        {
            if (BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }
            return value;
        }

        [CLSCompliant(false)]
        public static uint FromNetwork(uint value)
        {
            if (BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }
            return value;
        }

        public static long FromNetwork(long value)
        {
            if (BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }
            return value;
        }

        [CLSCompliant(false)]
        public static ulong FromNetwork(ulong value)
        {
            if (BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }
            return value;
        }

        public static short ToNetwork(short value)
        {
            if (BitConverter.IsLittleEndian)
                return FromNetwork(value);
            return value;
        }

        [CLSCompliant(false)]
        public static ushort ToNetwork(ushort value)
        {
            if (BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }
            return value;
        }

        public static int ToNetwork(int value)
        {
            if (BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }
            return value;
        }

        [CLSCompliant(false)]
        public static uint ToNetwork(uint value)
        {
            if (BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }
            return value;
        }

        public static long ToNetwork(long value)
        {
            if (BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }
            return value;
        }

        [CLSCompliant(false)]
        public static ulong ToNetwork(ulong value)
        {
            if (BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }
            return value;
        }

        public static short ToInt16(byte[] value, int startOffset)
        {
            return FromNetwork(BitConverter.ToInt16(value, startOffset));
        }

        public static short ToInt16(BucketBytes value, int startOffset)
        {
#if NETFRAMEWORK
            var b = value.Span.Slice(startOffset, sizeof(short)).ToArray();
            return FromNetwork(BitConverter.ToInt16(b, 0));
#else
            return FromNetwork(BitConverter.ToInt16(value.Span.Slice(startOffset)));
#endif
        }

        public static int ToInt32(byte[] value, int startOffset)
        {
            return FromNetwork(BitConverter.ToInt32(value, startOffset));
        }

        public static int ToInt32(BucketBytes value, int startOffset)
        {
#if NETFRAMEWORK
            var b = value.Span.Slice(startOffset, sizeof(int)).ToArray();
            return FromNetwork(BitConverter.ToInt32(b, 0));
#else
            return FromNetwork(BitConverter.ToInt32(value.Span.Slice(startOffset)));
#endif
        }

        public static long ToInt64(byte[] value, int startOffset)
        {
            return FromNetwork(BitConverter.ToInt64(value, startOffset));
        }

        public static long ToInt64(BucketBytes value, int startOffset)
        {
#if NETFRAMEWORK
            var b = value.Span.Slice(startOffset, sizeof(ulong)).ToArray();
            return FromNetwork(BitConverter.ToInt64(b, 0));
#else
            return FromNetwork(BitConverter.ToInt64(value.Span[startOffset..]));
#endif
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(byte[] value, int startOffset)
        {
            return FromNetwork(BitConverter.ToUInt16(value, startOffset));
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(BucketBytes value, int startOffset)
        {
#if NETFRAMEWORK
            var b = value.Span.Slice(startOffset, sizeof(ushort)).ToArray();
            return FromNetwork(BitConverter.ToUInt16(b, 0));
#else
            return FromNetwork(BitConverter.ToUInt16(value.Span.Slice(startOffset)));
#endif
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(byte[] value, int startOffset)
        {
            return FromNetwork(BitConverter.ToUInt32(value, startOffset));
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(BucketBytes value, int startOffset)
        {
#if NETFRAMEWORK
            var b = value.Span.Slice(startOffset, sizeof(uint)).ToArray();
            return FromNetwork(BitConverter.ToUInt32(b, 0));
#else
            return FromNetwork(BitConverter.ToUInt32(value.Span.Slice(startOffset)));
#endif
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(byte[] value, int startOffset)
        {
            return FromNetwork(BitConverter.ToUInt64(value, startOffset));
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(BucketBytes value, int startOffset)
        {
#if NETFRAMEWORK
            var b = value.Span.Slice(startOffset, sizeof(ulong)).ToArray();
            return FromNetwork(BitConverter.ToUInt64(b, 0));
#else
            return FromNetwork(BitConverter.ToUInt64(value.Span[startOffset..]));
#endif
        }
    }
}
