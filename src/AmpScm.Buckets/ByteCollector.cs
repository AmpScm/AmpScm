﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets
{
    public struct ByteCollector : IEnumerable<byte>, IEquatable<ByteCollector>, IReadOnlyCollection<byte>
    {
        IEnumerable<byte>? _bytes;
        ushort _expected;

        public ByteCollector()
        {
            _bytes = null;
            Count = 0;
            _expected = 0;
        }

        public ByteCollector(int expectedSize)
        {
            if (expectedSize < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedSize));

            Count = 0;
            _expected = (ushort)Math.Min(expectedSize, 32768);
            _bytes = null;
        }

        public bool IsEmpty => (Count == 0);

        public int Count { get; private set; }

        public bool SequenceEqual(Span<byte> sq)
        {
            if (_bytes is null)
                return sq.Length == 0;

            int i = 0;
            foreach (var v in (_expected > 0) ? _bytes.Take(Count) : _bytes)
            {
                if (i >= sq.Length)
                    return false;
                else if (v != sq[i])
                    return false;
                i++;
            }
            return i == sq.Length;
        }

        public void Append(BucketBytes bytes)
        {
            if (bytes.Length == 0)
                return;

            if (_expected > 0)
            {
                byte[]? bb = _bytes as byte[];

                if (bytes.Length + (bb?.Length ?? 0) <= _expected)
                {
                    _bytes = bb ??= new byte[_expected];

                    bytes.CopyTo(bb.AsSpan(Count));
                    Count += bytes.Length;
                    return;
                }
                else if (Count == 0)
                {
                    _bytes = bytes!.ToArray();
                    Count = bytes.Length;
                    _expected = 0;
                    return;
                }
                else
                {
                    _bytes = _bytes!.Take(Count);
                    _expected = 0;
                    // And fall through
                }
            }

            if (_bytes is null)
                _bytes = bytes.ToArray();
            else
                _bytes = _bytes.Concat(bytes.ToArray());

            Count += bytes.Length;
        }

        public void Append(byte[] bytes)
        {
            if (bytes is null || bytes.Length == 0)
                return;

            if (_expected > 0)
            {
                byte[]? bb = (byte[]?)_bytes;

                if (bytes.Length + (bb?.Length ?? 0) <= _expected)
                {
                    _bytes = bb ??= new byte[_expected];

                    bytes.CopyTo(bb.AsSpan(Count));
                    Count += bb.Length;

                    if (Count == _expected)
                        _expected = 0;

                    return;
                }

                _expected = 0;
                
                if (Count == 0)
                {
                    _bytes = bytes;
                    Count = bytes!.Length;
                    return;
                }
                else
                    _bytes = _bytes!.Take(Count);
            }


            if (_bytes is null)
                _bytes = bytes;
            else
                _bytes = _bytes.Concat(bytes!.ToArray());

            Count += bytes!.Length;
        }

        public void Append(byte b)
        {
            if (_expected > 0)
            {
                byte[]? bb = (byte[]?)_bytes;

                if (1+ (bb?.Length ?? 0) <= _expected)
                {
                    _bytes = bb ??= new byte[_expected];

                    bb[Count] = b;
                    Count += 1;
                    return;
                }

                _expected = 0;

                _bytes = _bytes!.Take(Count);
            }

            if (_bytes is null)
                _bytes = new byte[] { b };
            else
#if !NETFRAMEWORK || NET48_OR_GREATER
                _bytes = _bytes.Append(b);
#else
                _bytes = _bytes.Concat(new byte[]{b});
#endif
            Count += 1;
        }

        public byte[] ToArray()
        {
            if (Count == 0)
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

                if (Count > 0)
                {
                    byte[] bb = (byte[])_bytes!;

                    _bytes = _bytes!.Take(Count);
                    if (Count + appendBytes.Length <= bb.Length)
                    {
                        appendBytes.CopyTo(bb.AsSpan(Count));
                        return new BucketBytes(bb, 0, Count + appendBytes.Length);
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
                if (Count > 0)
                {
                    if (Count < _expected)
                        _bytes = _bytes!.Take(Count);
                }
                _expected = 0;
            }
        }

        public BucketBytes AsBytes()
        {
            if (_expected > 0 && Count > 0)
                return new BucketBytes((byte[])_bytes!, 0, Count);

            return ToArray();
        }

        public BucketBytes ToResultOrEof()
        {
            if (IsEmpty)
                return BucketBytes.Eof;
            else
                return ToArray();
        }

        public IEnumerator<byte> GetEnumerator()
        {
            if (Count == 0)
                return Enumerable.Empty<byte>().GetEnumerator();
            else if (_expected > 0)
                return _bytes!.Take(Count).GetEnumerator();
            else
                return _bytes!.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Equals(ByteCollector other)
        {
            if (Count != other.Count)
                return false;
            else if (Count == 0)
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

        public override int GetHashCode()
        {
            return Count;
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
