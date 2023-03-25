using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets
{
#pragma warning disable CA1710 // Identifiers should have correct suffix
    public struct ByteCollector : IEnumerable<byte>, IEquatable<ByteCollector>, IReadOnlyCollection<byte>
#pragma warning restore CA1710 // Identifiers should have correct suffix
    {
        private IEnumerable<byte>? _bytes;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ushort _expected;

        public ByteCollector()
        {
            _bytes = null;
            Length = 0;
            _expected = 0;
        }

        public ByteCollector(int expectedSize)
        {
            if (expectedSize < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedSize));

            Length = 0;
            _expected = (ushort)Math.Min(expectedSize, 32768);
            _bytes = null;
        }

        public readonly bool IsEmpty => (Length == 0);

        public int Length { get; private set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        readonly int IReadOnlyCollection<byte>.Count => Length;

        public readonly bool SequenceEqual(Span<byte> sq)
        {
            if (_bytes is null)
                return sq.Length == 0;

            if (sq.Length != Length)
                return false;

            int i = 0;
            foreach (byte v in (_expected > 0) ? _bytes.Take(Length) : _bytes)
            {
                if (v != sq[i++])
                    return false;
            }
            return true;
        }

        public void Append(ReadOnlyMemory<byte> bytes)
        {
            if (bytes.Length == 0)
                return;

            if (_expected > 0)
            {
                byte[]? bb = _bytes as byte[];

                if (bytes.Length + (bb?.Length ?? 0) <= _expected)
                {
                    _bytes = bb ??= new byte[_expected];

                    bytes.Span.CopyTo(bb.AsSpan(Length));
                    Length += bytes.Length;
                    return;
                }
                else if (Length == 0)
                {
                    _bytes = bytes!.ToArray();
                    Length = bytes.Length;
                    _expected = 0;
                    return;
                }
                else
                {
                    _bytes = _bytes!.Take(Length);
                    _expected = 0;
                    // And fall through
                }
            }

            if (_bytes is null)
                _bytes = bytes.ToArray();
            else
                _bytes = _bytes.Concat(bytes.ToArray());

            Length += bytes.Length;
        }

        public void Append(BucketBytes bytes)
            => Append(bytes.Memory);

        public void Clear()
        {
            Length = 0;

            if (_expected == 0)
            {
                _bytes = null;
                _expected = 256;
            }
        }

        public void Append(byte[] bytes)
        {
            if (bytes is null || bytes.Length == 0)
                return;

            if (_expected > 0)
            {
                byte[]? bb = (byte[]?)_bytes;

                if (bytes.Length + Length <= _expected)
                {
                    _bytes = bb ??= new byte[_expected];

                    bytes.CopyTo(bb.AsSpan(Length));
                    Length += bytes.Length;

                    if (Length == _expected)
                        _expected = 0;

                    return;
                }

                _expected = 0;
                
                if (Length == 0)
                {
                    _bytes = bytes;
                    Length = bytes!.Length;
                    return;
                }
                else
                    _bytes = _bytes!.Take(Length);
            }


            if (_bytes is null)
                _bytes = bytes;
            else
                _bytes = _bytes.Concat(bytes!.ToArray());

            Length += bytes!.Length;
        }

        public void Append(byte b)
        {
            if (_expected > 0)
            {
                byte[]? bb = (byte[]?)_bytes;

                if (1+ (bb?.Length ?? 0) <= _expected)
                {
                    _bytes = bb ??= new byte[_expected];

                    bb[Length] = b;
                    Length += 1;
                    return;
                }

                _expected = 0;

                _bytes = _bytes!.Take(Length);
            }

            if (_bytes is null)
                _bytes = new byte[] { b };
            else
#if !NETFRAMEWORK || NET48_OR_GREATER
                _bytes = _bytes.Append(b);
#else
                _bytes = _bytes.Concat(new byte[]{b});
#endif
            Length += 1;
        }

        public byte[] ToArray()
        {
            if (Length == 0)
                return Array.Empty<byte>();

            DropBuffer();
            if (_bytes is byte[] b)
                return b;
            else
            {
                b = _bytes!.ToArray();
                _bytes = b;
                return b;
            }
        }

        public BucketBytes AsBytes(BucketBytes appendBytes)
        {
            if (IsEmpty)
                return appendBytes;
            else if (appendBytes.IsEmpty)
                return IsEmpty ? BucketBytes.Empty : AsBytes();

            if (_expected > 0)
            {
                _expected = 0;

                if (Length > 0)
                {
                    byte[] bb = (byte[])_bytes!;

                    _bytes = _bytes!.Take(Length);
                    if (Length + appendBytes.Length <= bb.Length)
                    {
                        appendBytes.CopyTo(bb.AsSpan(Length));
                        return new BucketBytes(bb, 0, Length + appendBytes.Length);
                    }
                }
            }

            return _bytes!.Concat(appendBytes.ToArray()).ToArray();
        }

        public BucketBytes AsBytes(byte[] appendBytes)
        {
            DropBuffer();

            if (appendBytes is null || appendBytes.Length == 0)
                return IsEmpty ? BucketBytes.Empty : ToArray();
            else if (IsEmpty)
                return appendBytes;

            return _bytes!.Concat(appendBytes).ToArray();
        }

        public BucketBytes AsBytes(byte appendByte)
        {
            if (IsEmpty)
                return new byte[] { appendByte };

            DropBuffer();

#if !NETFRAMEWORK || NET48_OR_GREATER
            return _bytes!.Append(appendByte).ToArray();
#else
            return _bytes!.Concat(new byte[]{appendByte}).ToArray();
#endif
        }

        private void DropBuffer()
        {
            if (_expected > 0)
            {
                if (Length > 0)
                {
                    if (Length < _expected)
                        _bytes = _bytes!.Take(Length);
                }
                _expected = 0;
            }
        }

        public BucketBytes AsBytes()
        {
            if (_expected > 0 && Length > 0)
                return new BucketBytes((byte[])_bytes!, 0, Length);

            return ToArray();
        }

        public BucketBytes ToResultOrEof()
        {
            if (IsEmpty)
                return BucketBytes.Eof;
            else
                return ToArray();
        }

        public readonly IEnumerator<byte> GetEnumerator()
        {
            if (Length == 0)
                return Enumerable.Empty<byte>().GetEnumerator();
            else if (_expected > 0)
                return _bytes!.Take(Length).GetEnumerator();
            else
                return _bytes!.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Equals(ByteCollector other)
        {
            if (Length != other.Length)
                return false;
            else if (Length == 0)
                return true;
            else
            {
                DropBuffer();
                other.DropBuffer();
                return _bytes!.SequenceEqual(other._bytes!);
            }
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is ByteCollector bc)
                return Equals(bc);
            return false;
        }

        public override readonly int GetHashCode()
        {
            return Length;
        }

        public void CopyTo(Span<byte> buffer)
        {
            // TODO: Optimize :-)
            AsBytes().CopyTo(buffer);
        }

        public ReadOnlyMemory<byte> ToMemory()
        {
            return new(ToArray());
        }

        public static bool operator ==(ByteCollector left, ByteCollector right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ByteCollector left, ByteCollector right)
        {
            return !(left == right);
        }
    }
}
