using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace AmpScm.Buckets
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public partial struct BucketBytes : IEquatable<BucketBytes>, IValueOrEof<ReadOnlyMemory<byte>>
    {
        ReadOnlyMemory<byte> _data;
        readonly bool _eof;

        public BucketBytes(ReadOnlyMemory<byte> data)
        {
            _data = data;
            _eof = false;
        }

        public BucketBytes(byte[] array, int start, int length)
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));

            _data = new ReadOnlyMemory<byte>(array, start, length);
            _eof = false;
        }

        private BucketBytes(bool eof)
        {
            _data = ReadOnlyMemory<byte>.Empty;
            _eof = eof;
        }

        public override bool Equals(object? obj)
        {
            if (obj is BucketBytes bb)
                return Equals(bb);

            return base.Equals(obj);
        }

        public bool Equals(BucketBytes other)
        {
            return _data.Equals(other._data) && _eof == other._eof;
        }

        public override int GetHashCode()
        {
            return _data.GetHashCode();
        }

        public int Length => _data.Length;
        public bool IsEof => _eof;
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
        public static readonly BucketBytes Eof = new BucketBytes(true);


        public void CopyTo(Memory<byte> destination) => Span.CopyTo(destination.Span);

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


        string DebuggerDisplay
        {
            get
            {
                if (IsEof)
                    return "<EOF>";
                else
                    return $"Length={Length}, Data='{ToASCIIString(0, Math.Min(Length, 100))}'";
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
