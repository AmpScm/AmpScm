using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AmpScm.Buckets
{
    internal interface IValueOrEof<T> where T : struct
    {
        public T Value { get; }
        public bool IsEof { get; }
    }

    public readonly struct ValueOrEof : IEquatable<ValueOrEof>
    {
        public static ValueOrEof Eof => default;

        public override bool Equals(object? obj)
        {
            return obj is ValueOrEof;
        }

        public override int GetHashCode()
        {
            return 1;
        }

        public static bool operator ==(ValueOrEof left, ValueOrEof right)
        {
            return true;
        }

        public static bool operator !=(ValueOrEof left, ValueOrEof right)
        {
            return false;
        }

        public bool Equals(ValueOrEof other)
        {
            return true;
        }
    }

    [DebuggerDisplay("Value={Value}, Eof={IsEof}")]
    public readonly struct ValueOrEof<T> : IValueOrEof<T>, IEquatable<ValueOrEof<T>>
        where T : struct
    {
        private readonly T _value;
        private readonly bool _isEof;

        public ValueOrEof(T value)
        {
            _value = value;
            _isEof = false;
        }

        public ValueOrEof(ValueOrEof eof)
        {
            _value = default;
            _isEof = true;
        }

        public T Value
        {
            get
            {
                if (_isEof)
                    throw new InvalidOperationException("EOF");
                return _value;
            }
        }

        public bool IsEof => _isEof;

#pragma warning disable CA2225 // Operator overloads have named alternates
        public static implicit operator ValueOrEof<T>(T value) => new ValueOrEof<T>(value);
#pragma warning restore CA2225 // Operator overloads have named alternates

        public bool Equals(ValueOrEof<T> other)
        {
            if (other._isEof != _isEof)
                return false;
            else
                return _value.Equals(other._value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is ValueOrEof<T> v)
                return Equals(v);
            return false;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode() ^ (_isEof ? 77 : 0);
        }

        public static bool operator ==(ValueOrEof<T> left, ValueOrEof<T> right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(ValueOrEof<T> left, ValueOrEof<T> right)
        {
            return !(left == right);
        }

#pragma warning disable CA2225 // Operator overloads have named alternates
        public static implicit operator ValueOrEof<T>(ValueOrEof eof)
#pragma warning restore CA2225 // Operator overloads have named alternates
        {
            return new ValueOrEof<T>(eof);
        }
    }
}
