using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets
{
    [DebuggerDisplay("{DebuggerDisplay,nq}", Name ="Bytes")]
    public readonly partial struct BucketBytes : IEquatable<BucketBytes>, IValueOrEof<ReadOnlyMemory<byte>>
    {
        readonly ReadOnlyMemory<byte> _data;

        public BucketBytes(ReadOnlyMemory<byte> data)
        {
            _data = data;
        }

        public BucketBytes(byte[] array, int start, int length)
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));

            _data = new ReadOnlyMemory<byte>(array, start, length);
        }

        private BucketBytes(bool eof)
        {
            if (eof)
                _data = BucketBytes.Eof._data;
            else
                _data = BucketBytes.Empty._data;
        }

        public override bool Equals(object? obj)
        {
            if (obj is BucketBytes bb)
                return Equals(bb);

            return base.Equals(obj);
        }

        public bool Equals(BucketBytes other)
        {
            return _data.Equals(other._data);
        }

        public override int GetHashCode()
        {
            return _data.GetHashCode();
        }

        public int Length => _data.Length;
        public bool IsEof => Length == 0 && _data.Equals(Eof._data);
        public bool IsEmpty => _data.IsEmpty;

        public ReadOnlySpan<byte> Span => _data.Span;

        public ReadOnlyMemory<byte> Memory => _data;

        public BucketBytes Slice(int start)
        {
            if (start == Length)
                return default;
            return new BucketBytes(_data.Slice(start));
        }

        public BucketBytes Slice(int start, int length)
        {
            return new BucketBytes(_data.Slice(start, length));
        }

        public byte[] ToArray()
        {
            return _data.ToArray();
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
        public static implicit operator BucketBytes(byte[] array)
#pragma warning restore CA2225 // Operator overloads have named alternates
        {
            return new BucketBytes(array);
        }

#pragma warning disable CA2225 // Operator overloads have named alternates
        public static implicit operator ValueTask<BucketBytes>(BucketBytes v)
#pragma warning restore CA2225 // Operator overloads have named alternates
        {
            return new ValueTask<BucketBytes>(v);
        }

        public static readonly BucketBytes Empty = new BucketBytes(false);
        public static readonly BucketBytes Eof = new BucketBytes(new ReadOnlyMemory<byte>((byte[])new byte[] {(byte)'E', (byte)'O', (byte)'F'}.Clone(), 3, 0));


        public void CopyTo(Memory<byte> destination) => Span.CopyTo(destination.Span);
        public void CopyTo(byte[] array, int index) => Span.CopyTo(array.AsSpan(index));

        public byte this[int index] => _data.Span[index];

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

        ReadOnlyMemory<byte> IValueOrEof<ReadOnlyMemory<byte>>.Value => _data;


        public int IndexOf(byte value)
        {
            return _data.Span.IndexOf(value);
        }

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


        string DebuggerDisplay
        {
            get
            {
                if (IsEof)
                    return "<EOF>";
                else
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat(CultureInfo.InvariantCulture, "Length={0}, Data=\"", Length);

                    foreach(var b in Span)
                    {
                        if (b > 0 && b < 128 && !char.IsControl((char)b))
                            sb.Append((char)b);
                        else switch(b)
                            {
                                case 0:
                                    sb.Append("\\0");
                                    break;
                                case (byte)'\n':
                                    sb.Append("\\n");
                                    break;
                                case (byte)'\t':
                                    sb.Append("\\t");
                                    break;
                                case (byte)'\r':
                                    sb.Append("\\r");
                                    break;
                                default:
                                    sb.AppendFormat(CultureInfo.InvariantCulture, "\\x{0:X2}", b);
                                    break;
                            }

                        if (sb.Length > 120)
                        {
                            sb.Append("...");
                            return sb.ToString();
                        }
                    }
                    sb.Append('\"');
                    return sb.ToString();
                }
            }
        }

        #region ZLib optimization. Our ZLib doesn't use Span<> and Memory<> yet, but let's reuse byte[] directly instead of copying
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
            if (_data.Length == 0)
                return (Array.Empty<byte>(), 0);

            var (ob, index) = ReadOnlyMemoryExpander(_data);

            if (ob is byte[] arr)
                return (arr, index);

            byte[] data = ToArray();

            return (data, 0);
        }

        internal void Deconstruct(out byte[]? array, out int offset)
        {
            if (_data.Length == 0)
            {
                array = null;
                offset = 0;
                return;
            }

            object ob;
            (ob, offset) = ReadOnlyMemoryExpander(_data);
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
