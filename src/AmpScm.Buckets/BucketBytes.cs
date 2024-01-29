using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets;

public readonly partial struct BucketBytes : IEquatable<BucketBytes>, IValueOrEof<ReadOnlyMemory<byte>>
{
    public ReadOnlyMemory<byte> Memory { get; }

    public BucketBytes(ReadOnlyMemory<byte> data)
    {
        Memory = data;
    }

    public BucketBytes(byte[] array, int start, int length)
    {
        if (array is null && length > 0)
            throw new ArgumentNullException(nameof(array));

        Memory = new ReadOnlyMemory<byte>(array, start, length);
    }

    public override bool Equals(object? obj)
    {
        if (obj is BucketBytes bb)
            return Equals(bb);

        return false;
    }

    public bool Equals(BucketBytes other)
    {
        return Memory.Equals(other.Memory);
    }

    public override int GetHashCode()
    {
        return Memory.GetHashCode();
    }

    public int Length => Memory.Length;
    public bool IsEof => Length == 0 && Memory.Equals(Eof.Memory);
    public bool IsEmpty => Memory.IsEmpty;

    public ReadOnlySpan<byte> Span => Memory.Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BucketBytes Slice(int start)
    {
        if (Length == 0 || start == 0)
            return this; // Keep EOF

        return Memory.Slice(start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BucketBytes Slice(int start, int length)
    {
        if (length == 0 && Length == 0 && start == 0)
            return this; // Keep EOF

        return Memory.Slice(start, length);
    }

    public BucketBytes Slice(int start, BucketEol untilEol)
    {
        return Slice(start, Length - start - untilEol.CharCount());
    }

    public BucketBytes Slice(BucketEol untilEol)
    {
        return Slice(0, Length - untilEol.CharCount());
    }


    public byte[] ToArray()
    {
        return Memory.ToArray();
    }

#pragma warning disable CA2225 // Operator overloads have named alternates
    public static implicit operator BucketBytes(ArraySegment<byte> segment)
#pragma warning restore CA2225 // Operator overloads have named alternates
    {
        return new BucketBytes(segment);
    }

#pragma warning disable CA2225 // Operator overloads have named alternates
    public static implicit operator BucketBytes(ReadOnlyMemory<byte> segment)
#pragma warning restore CA2225 // Operator overloads have named alternates
    {
        return new BucketBytes(segment);
    }

#pragma warning disable CA2225 // Operator overloads have named alternates
    public static implicit operator BucketBytes(Memory<byte> segment)
#pragma warning restore CA2225 // Operator overloads have named alternates
    {
        return new BucketBytes(segment);
    }

#pragma warning disable CA2225 // Operator overloads have named alternates
    public static implicit operator ReadOnlyMemory<byte>(BucketBytes bytes)
#pragma warning restore CA2225 // Operator overloads have named alternates
    {
        return bytes.Memory;
    }

#pragma warning disable CA2225 // Operator overloads have named alternates
    public static implicit operator BucketBytes(byte[] array)
#pragma warning restore CA2225 // Operator overloads have named alternates
    {
        return new BucketBytes(array);
    }

#pragma warning disable CA2225 // Operator overloads have named alternates
    public static implicit operator ValueTask<BucketBytes>(BucketBytes v)
#pragma warning restore CA2225 // Operator overloads have named alternates
    {
        return new (v);
    }

    public static readonly BucketBytes Empty;// = default;
    // Clone to make sure data is not shared
    public static readonly BucketBytes Eof = new BucketBytes(new ReadOnlyMemory<byte>((byte[])"eOf"u8.ToArray().Clone(), 3, 0));


    public void CopyTo(Memory<byte> destination) => Span.CopyTo(destination.Span);
    public void CopyTo(Span<byte> destination) => Span.CopyTo(destination);
    public void CopyTo(byte[] array, int index) => Span.CopyTo(array.AsSpan(index));
    public void CopyTo(byte[] array) => Span.CopyTo(array.AsSpan());

    public byte this[int index] => Memory.Span[index];

    /// <summary>
    /// Copies the contents of the readonly-only memory into the destination. If the source
    /// and destination overlap, this method behaves as if the original values are in
    /// a temporary location before the destination is overwritten.
    ///
    /// <returns>If the destination is shorter than the source, this method
    /// return false and no data is written to the destination.</returns>
    /// </summary>
    /// <param name="destination">The span to copy items into.</param>
    public bool TryCopyTo(Memory<byte> destination) => Span.TryCopyTo(destination.Span);

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    ReadOnlyMemory<byte> IValueOrEof<ReadOnlyMemory<byte>>.Value => Memory;

    /// <inheritdoc cref="MemoryExtensions.IndexOf{T}(ReadOnlySpan{T}, T)"/>
    public int IndexOf(byte value)
    {
        return Memory.Span.IndexOf(value);
    }

    /// <inheritdoc cref="MemoryExtensions.IndexOfAny{T}(ReadOnlySpan{T}, T, T)"/>
    public int IndexOfAny(byte value0, byte value1)
    {
        return Memory.Span.IndexOfAny(value0, value1);
    }

    /// <inheritdoc cref="MemoryExtensions.IndexOfAny{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>
    public int IndexOfAny(ReadOnlySpan<byte> values)
    {
        return Memory.Span.IndexOfAny(values);
    }

    /// <inheritdoc cref="MemoryExtensions.IndexOfAny{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>
    public int IndexOfAny(params byte[] values)
    {
        return Memory.Span.IndexOfAny(values);
    }

    /// <inheritdoc cref="MemoryExtensions.IndexOf{T}(ReadOnlySpan{T}, T)"/>
    public int IndexOf(byte value, int startOffset)
    {
        int s = Span.Slice(startOffset).IndexOf(value);

        if (s >= 0)
            return s + startOffset;
        else
            return s; // -1
    }

    /// <inheritdoc cref="System.Linq.Enumerable.All{TSource}(IEnumerable{TSource}, Func{TSource, bool})"/>
    public bool All(Func<byte, bool> predicate)
    {
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        foreach (byte b in Span)
        {
            if (!predicate(b))
                return false;
        }

        return true;
    }

    /// <inheritdoc cref="System.Linq.Enumerable.Any{TSource}(IEnumerable{TSource}, Func{TSource, bool})"/>
    public bool Any(Func<byte, bool> predicate)
    {
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        foreach (byte b in Span)
        {
            if (predicate(b))
                return true;
        }

        return false;
    }

    #region ZLib optimization. Our ZLib doesn't use Span<> and Memory<> yet, but let's reuse byte[] directly instead of copying

    internal static (byte[]?, int) ExpandToArray(ReadOnlyMemory<byte> data)
    {
        if (data.Length == 0)
            return (Array.Empty<byte>(), 0);

        if (MemoryMarshal.TryGetArray(data, out var segment))
        {
            return (segment.Array, segment.Offset);
        }

        return (null, -1);
    }

    internal (byte[] Bytes, int Index) ExpandToArray()
    {
        if (Memory.Length == 0)
            return (Array.Empty<byte>(), 0);

        if (MemoryMarshal.TryGetArray(Memory, out var segment))
        {
            return (segment.Array!, segment.Offset);
        }

        return (ToArray(), 0);
    }

    internal void Deconstruct(out byte[]? array, out int offset)
    {
        if (Memory.Length > 0 && MemoryMarshal.TryGetArray(Memory, out var segment))
        {
            array = segment.Array;
            offset = segment.Offset;
        }
        else
        {
            array = null;
            offset = 0;
            return;
        }
    }

    public static bool operator ==(BucketBytes left, BucketBytes right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(BucketBytes left, BucketBytes right)
    {
        return !(left == right);
    }
    #endregion
}
