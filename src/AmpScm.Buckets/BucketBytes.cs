using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets
{
    [DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
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
        public static readonly BucketBytes Eof = new BucketBytes(new ReadOnlyMemory<byte>((byte[])new byte[] { (byte)'e', (byte)'O', (byte)'f' }.Clone(), 3, 0));


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
            var s = Span.Slice(startOffset).IndexOf(value);

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

            foreach (var b in Span)
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

            foreach (var b in Span)
            {
                if (predicate(b))
                    return true;
            }

            return false;
        }


        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string DebuggerDisplay
        {
            get
            {
                if (IsEof)
                    return "<EOF>";
                else
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat(CultureInfo.InvariantCulture, "Length={0}, Data=", Length);

                    sb.Append(this.AsDebuggerDisplay());
                    return sb.ToString();
                }
            }
        }

        #region ZLib optimization. Our ZLib doesn't use Span<> and Memory<> yet, but let's reuse byte[] directly instead of copying
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        static Func<ReadOnlyMemory<byte>, (object, int)> ReadOnlyMemoryExpander { get; } = FindReadOnlyMemoryExpander();

        static Func<ReadOnlyMemory<byte>, (object, int)> FindReadOnlyMemoryExpander()
        {
            ParameterExpression p = Expression.Parameter(typeof(ReadOnlyMemory<byte>), "x");

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
            var c = Expression.New(typeof((object, int)).GetConstructors().OrderByDescending(x => x.GetParameters().Length).First(),
                       Expression.Field(p, "_object"),
                       Expression.Field(p, "_index"));
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
            return Expression.Lambda<Func<ReadOnlyMemory<byte>, (object, int)>>(c, p).Compile();
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        static Func<Memory<byte>, (object, int)> MemoryExpander { get; } = FindMemoryExpander();

        static Func<Memory<byte>, (object, int)> FindMemoryExpander()
        {
            ParameterExpression p = Expression.Parameter(typeof(Memory<byte>), "x");

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
            var c = Expression.New(typeof((object, int)).GetConstructors().OrderByDescending(x => x.GetParameters().Length).First(),
                       Expression.Field(p, "_object"),
                       Expression.Field(p, "_index"));
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
            return Expression.Lambda<Func<Memory<byte>, (object, int)>>(c, p).Compile();
        }

        internal static (byte[]?, int) ExpandToArray(Memory<byte> _data)
        {
            if (_data.Length == 0)
                return (Array.Empty<byte>(), 0);

            var (ob, index) = MemoryExpander(_data);

            if (ob is byte[] arr)
                return (arr, index);
            else
                return (null, -1);
        }

        internal static (byte[]?, int) ExpandToArray(ReadOnlyMemory<byte> _data)
        {
            if (_data.Length == 0)
                return (Array.Empty<byte>(), 0);

            var (ob, index) = ReadOnlyMemoryExpander(_data);

            if (ob is byte[] arr)
                return (arr, index);
            else
                return (null, -1);
        }

        internal (byte[] Bytes, int Index) ExpandToArray()
        {
            if (Memory.Length == 0)
                return (Array.Empty<byte>(), 0);

            var (ob, index) = ReadOnlyMemoryExpander(Memory);

            if (ob is byte[] arr)
                return (arr, index);

            byte[] data = ToArray();

            return (data, 0);
        }

        internal void Deconstruct(out byte[]? array, out int offset)
        {
            if (Memory.Length == 0)
            {
                array = null;
                offset = 0;
                return;
            }

            object ob;
            (ob, offset) = ReadOnlyMemoryExpander(Memory);
            array = ob as byte[];
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

}
