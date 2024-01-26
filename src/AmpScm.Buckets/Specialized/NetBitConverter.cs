using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized;

/// <summary>
/// Like <see cref="BitConverter"/>, but then for values in network order
/// </summary>
public static class NetBitConverter
{
    /// <inheritdoc cref="BitConverter.GetBytes(short)" />
    public static byte[] GetBytes(short value)
    {
        return BitConverter.GetBytes(ToNetwork(value));
    }

    /// <inheritdoc cref="BitConverter.GetBytes(ushort)" />
    [CLSCompliant(false)]
    public static byte[] GetBytes(ushort value)
    {
        return BitConverter.GetBytes(ToNetwork(value));
    }

    /// <inheritdoc cref="BitConverter.GetBytes(int)" />
    public static byte[] GetBytes(int value)
    {
        return BitConverter.GetBytes(ToNetwork(value));
    }

    /// <inheritdoc cref="BitConverter.GetBytes(uint)" />
    [CLSCompliant(false)]
    public static byte[] GetBytes(uint value)
    {
        return BitConverter.GetBytes(ToNetwork(value));
    }

    /// <inheritdoc cref="BitConverter.GetBytes(long)" />
    public static byte[] GetBytes(long value)
    {
        return BitConverter.GetBytes(ToNetwork(value));
    }

    /// <inheritdoc cref="BitConverter.GetBytes(ulong)" />
    [CLSCompliant(false)]
    public static byte[] GetBytes(ulong value)
    {
        return BitConverter.GetBytes(ToNetwork(value));
    }

    /// <summary>
    /// Converts a short read as system ordering to network ordering
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <remarks>On a typical x86/x64 cpu this swaps the bytes. On a system that is in network ordering, it doesn't</remarks>
    public static short FromNetwork(short value)
    {
        if (BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }
        return value;
    }

    /// <summary>
    /// Converts an ushort read as system ordering to network ordering
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <remarks>On a typical x86/x64 cpu this swaps the bytes. On a system that is in network ordering, it doesn't</remarks>
    [CLSCompliant(false)]
    public static ushort FromNetwork(ushort value)
    {
        if (BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }
        return value;
    }

    /// <summary>
    /// Converts an integer read as system ordering to network ordering
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <remarks>On a typical x86/x64 cpu this swaps the bytes. On a system that is in network ordering, it doesn't</remarks>
    public static int FromNetwork(int value)
    {
        if (BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }
        return value;
    }

    /// <summary>
    /// Converts an unsigned integer read as system ordering to network ordering
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <remarks>On a typical x86/x64 cpu this swaps the bytes. On a system that is in network ordering, it doesn't</remarks>
    [CLSCompliant(false)]
    public static uint FromNetwork(uint value)
    {
        if (BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }
        return value;
    }

    /// <summary>
    /// Converts a long read as system ordering to network ordering
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <remarks>On a typical x86/x64 cpu this swaps the bytes. On a system that is in network ordering, it doesn't</remarks>
    public static long FromNetwork(long value)
    {
        if (BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }
        return value;
    }

    /// <summary>
    /// Converts an unsigned long read as system ordering to network ordering
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <remarks>On a typical x86/x64 cpu this swaps the bytes. On a system that is in network ordering, it doesn't</remarks>
    [CLSCompliant(false)]
    public static ulong FromNetwork(ulong value)
    {
        if (BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }
        return value;
    }

    /// <summary>
    /// Converts a short in network ordering to system ordering
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <remarks>On a typical x86/x64 cpu this swaps the bytes. On a system that is in network ordering, it doesn't</remarks>
    public static short ToNetwork(short value)
    {
        if (BitConverter.IsLittleEndian)
            return FromNetwork(value);
        return value;
    }

    /// <summary>
    /// Converts an unsigned short in network ordering to system ordering
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <remarks>On a typical x86/x64 cpu this swaps the bytes. On a system that is in network ordering, it doesn't</remarks>
    [CLSCompliant(false)]
    public static ushort ToNetwork(ushort value)
    {
        if (BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }
        return value;
    }

    /// <summary>
    /// Converts an integer in network ordering to system ordering
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <remarks>On a typical x86/x64 cpu this swaps the bytes. On a system that is in network ordering, it doesn't</remarks>
    public static int ToNetwork(int value)
    {
        if (BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }
        return value;
    }

    /// <summary>
    /// Converts an unsigned integer in network ordering to system ordering
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <remarks>On a typical x86/x64 cpu this swaps the bytes. On a system that is in network ordering, it doesn't</remarks>
    [CLSCompliant(false)]
    public static uint ToNetwork(uint value)
    {
        if (BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }
        return value;
    }

    /// <summary>
    /// Converts a long in network ordering to system ordering
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <remarks>On a typical x86/x64 cpu this swaps the bytes. On a system that is in network ordering, it doesn't</remarks>
    public static long ToNetwork(long value)
    {
        if (BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }
        return value;
    }

    /// <summary>
    /// Converts an unsigned long in network ordering to system ordering
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <remarks>On a typical x86/x64 cpu this swaps the bytes. On a system that is in network ordering, it doesn't</remarks>
    [CLSCompliant(false)]
    public static ulong ToNetwork(ulong value)
    {
        if (BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }
        return value;
    }

    /// <inheritdoc cref="BitConverter.ToInt16(byte[], int)" />
    public static short ToInt16(byte[] value, int startOffset)
    {
        return BinaryPrimitives.ReadInt16BigEndian(value.AsSpan(startOffset));
    }

    /// <inheritdoc cref="BitConverter.ToInt16(byte[], int)"/>
    public static short ToInt16(BucketBytes value, int startOffset)
    {
        return BinaryPrimitives.ReadInt16BigEndian(value.Span.Slice(startOffset));
    }

    /// <inheritdoc cref="BitConverter.ToInt32(byte[], int)"/>
    public static int ToInt32(byte[] value, int startOffset)
    {
        return BinaryPrimitives.ReadInt32BigEndian(value.AsSpan(startOffset));
    }

    /// <inheritdoc cref="BitConverter.ToInt32(byte[], int)"/>
    public static int ToInt32(BucketBytes value, int startOffset)
    {
        return BinaryPrimitives.ReadInt32BigEndian(value.Span.Slice(startOffset));
    }

    /// <inheritdoc cref="BitConverter.ToInt64(byte[], int)"/>
    public static long ToInt64(byte[] value, int startOffset)
    {
        return BinaryPrimitives.ReadInt64BigEndian(value.AsSpan(startOffset));
    }

    /// <inheritdoc cref="BitConverter.ToInt64(byte[], int)"/>
    public static long ToInt64(BucketBytes value, int startOffset)
    {
        return BinaryPrimitives.ReadInt64BigEndian(value.Span.Slice(startOffset));
    }

    /// <inheritdoc cref="BitConverter.ToUInt16(byte[], int)"/>
    [CLSCompliant(false)]
    public static ushort ToUInt16(byte[] value, int startOffset)
    {
        return BinaryPrimitives.ReadUInt16BigEndian(value.AsSpan(startOffset));
    }

    /// <inheritdoc cref="BitConverter.ToUInt16(byte[], int)"/>
    [CLSCompliant(false)]
    public static ushort ToUInt16(BucketBytes value, int startOffset)
    {
        return BinaryPrimitives.ReadUInt16BigEndian(value.Span.Slice(startOffset));
    }

    /// <inheritdoc cref="BitConverter.ToUInt32(byte[], int)"/>
    [CLSCompliant(false)]
    public static uint ToUInt32(byte[] value, int startOffset)
    {
        return BinaryPrimitives.ReadUInt32BigEndian(value.AsSpan(startOffset));
    }

    /// <inheritdoc cref="BitConverter.ToUInt32(byte[], int)"/>
    [CLSCompliant(false)]
    public static uint ToUInt32(BucketBytes value, int startOffset)
    {
        return BinaryPrimitives.ReadUInt32BigEndian(value.Span.Slice(startOffset));
    }

    /// <inheritdoc cref="BitConverter.ToUInt64(byte[], int)"/>
    [CLSCompliant(false)]
    public static ulong ToUInt64(byte[] value, int startOffset)
    {
        return BinaryPrimitives.ReadUInt64BigEndian(value.AsSpan(startOffset));
    }

    /// <inheritdoc cref="BitConverter.ToUInt64(byte[], int)"/>
    [CLSCompliant(false)]
    public static ulong ToUInt64(BucketBytes value, int startOffset)
    {
        return BinaryPrimitives.ReadUInt64BigEndian(value.Span.Slice(startOffset));
    }
}
