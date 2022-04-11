using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using AmpScm.Buckets;

[assembly: CLSCompliant(true)]

namespace AmpScm.Git
{
    public enum GitIdType
    {
        None = 0,
        Sha1 = 1,
        Sha256 = 2,
    }

    [DebuggerDisplay("{ToString(),nq} ({Type}")]
    public sealed class GitId : IEquatable<GitId>, IComparable<GitId>, IFormattable
    {
        byte[] _bytes;
        int _offset;
        public GitIdType Type { get; }

        public ReadOnlyMemory<byte> Hash
        {
            get => new(_bytes, _offset, HashLength(Type));
        }

        /// <summary>
        /// Creates a new <see cref="GitId"/> of the specified hash with the specified hash
        /// </summary>
        /// <param name="type"></param>
        /// <param name="hash"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <remarks>The hash is referenced as is, so changing the hash after adding it breaks this <see cref="GitId"/></remarks>
        public GitId(GitIdType type, byte[] hash)
        {
            if (hash is null)
                throw new ArgumentNullException(nameof(hash));
            else if (type <= GitIdType.None || type > GitIdType.Sha256)
                throw new ArgumentOutOfRangeException(nameof(type));
            else if (hash.Length != HashLength(type))
                throw new ArgumentOutOfRangeException(nameof(hash));

            Type = type;
            _bytes = hash;
        }

        /// <summary>
        /// Creates a new <see cref="GitId"/> of type <see cref="GitIdType.Sha1"/> when a 40 character string is provided, or
        /// a new <see cref="GitId"/> of type <see cref="GitIdType.Sha256"/> when a 64 character string is provided.
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public GitId(string value)
            : this(
                    (value?.Length ?? 0) switch
                    {
                        40 => GitIdType.Sha1,
                        64 => GitIdType.Sha256,
                        0 => throw new ArgumentNullException(nameof(value)),
                        _ => throw new ArgumentOutOfRangeException(nameof(value))
                    },
                    GitId.TryParse(value!, out var id) ? id._bytes : throw new ArgumentOutOfRangeException(nameof(value)))
        {

        }

        /// <summary>
        /// Internal helper to optimize using indexes
        /// </summary>
        /// <param name="type"></param>
        /// <param name="hash"></param>
        /// <param name="offset"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        GitId(GitIdType type, byte[] hash, int offset)
        {
            Type = type;
            _bytes = hash;
            _offset = offset;

            if (offset + HashLength(type) > hash.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
        }

        /// <summary>
        /// Creates GitId that uses a location inside an existing array.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="hash"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        /// <remarks>Only use this if you are 100% sure the source array doesn't change, as changing it will change the objectid
        /// and break things like equals and hashing</remarks>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public static GitId FromByteArrayOffset(GitIdType type, byte[] hash, int offset)
        {
            if (hash is null)
                throw new ArgumentNullException(nameof(hash));

            return new GitId(type, hash, offset);
        }

        public override bool Equals(object? obj)
        {
            return base.Equals(obj as GitId);
        }

        public bool Equals(GitId? other)
        {
            if (other is null)
                return false;

            if (other.Type != Type)
                return false;

            return HashCompare(other) == 0;
        }

        public int HashCompare(GitId other)
        {
            if (other is null)
                return 1;

            if (other.Type != Type)
                return (int)Type - (int)other.Type;

            return Hash.Span.SequenceCompareTo(other.Hash.Span);
        }

        public static bool TryParse(string idString, [NotNullWhen(true)] out GitId? id)
        {
            if (idString is null)
                throw new ArgumentNullException(nameof(idString));

            if ((idString.Length & 0x3) != 0 && (char.IsWhiteSpace(idString, 0) || char.IsWhiteSpace(idString, idString.Length - 1)))
                idString = idString.Trim();

            if (idString.Length == 40 && TryStringToByteArray(idString, out var b1))
            {
                id = new GitId(GitIdType.Sha1, b1);
                return true;
            }
            else if (idString.Length == 64 && TryStringToByteArray(idString, out var b2))
            {
                id = new GitId(GitIdType.Sha256, b2);
                return true;
            }
            else
            {
                id = null!;
                return false;
            }
        }

        static bool TryGetHex(ushort c1, ushort c2, out byte b)
        {
            if (c1 >= '0' && c1 <= '9')
                b = (byte)(c1 - '0');
            else if (c1 >= 'a' && c1 <= 'f')
                b = (byte)(c1 - 'a' + 10);
            else if (c1 >= 'A' && c1 <= 'F')
                b = (byte)(c1 - 'A' + 10);
            else
            {
                b = 0;
                return false;
            }

            b = (byte)(b << 4);

            if (c2 >= '0' && c2 <= '9')
                b |= (byte)(c2 - '0');
            else if (c2 >= 'a' && c2 <= 'f')
                b |= (byte)(c2 - 'a' + 10);
            else if (c2 >= 'A' && c2 <= 'F')
                b |= (byte)(c2 - 'A' + 10);
            else
            {
                b = 0;
                return false;
            }

            return true;
        }

        private static bool TryStringToByteArray(string idString, [NotNullWhen(true)] out byte[]? bytes)
        {
            if (idString is null)
                throw new ArgumentNullException(nameof(idString));
            else if (0 != (idString.Length & 1))
            {
                bytes = null;
                return false;
            }

            bytes = new byte[idString.Length / 2];

            for (int i = 0; i < idString.Length; i += 2)
            {
                if (TryGetHex(idString[i], idString[i + 1], out var b))
                {
                    bytes[i / 2] = b;
                }
                else
                    return false;
            }
            return true;
        }

        private static bool TryASCIIToByteArray(BucketBytes idBuffer, [NotNullWhen(true)] out byte[]? bytes)
        {
            if (0 != (idBuffer.Length & 1))
            {
                bytes = null;
                return false;
            }

            bytes = new byte[idBuffer.Length / 2];

            for (int i = 0; i < idBuffer.Length; i += 2)
            {
                if (TryGetHex(idBuffer[i], idBuffer[i + 1], out var b))
                {
                    bytes[i / 2] = b;
                }
                else
                    return false;
            }
            return true;
        }

        public static bool TryParse(BucketBytes idBuffer, [NotNullWhen(true)] out GitId? id)
        {
            int length = idBuffer.Length;
            if ((length & 0x3) != 0)
            {
                while ((length & 0x3) != 0)
                {
                    byte c = idBuffer[length - 1];

                    switch (c)
                    {
                        case (byte)'\0':
                        case (byte)'\n':
                        case (byte)' ':
                        case (byte)'\r':
                        case (byte)'\t':
                            length--;
                            break;
                        default:
                            id = default!;
                            return false;
                    }
                }
                idBuffer = idBuffer.Slice(0, length);
            }

            if (length == 40 && TryASCIIToByteArray(idBuffer, out var b1))
            {
                id = new GitId(GitIdType.Sha1, b1);
                return true;
            }
            else if (length == 64 && TryASCIIToByteArray(idBuffer, out var b2))
            {
                id = new GitId(GitIdType.Sha256, b2);
                return true;
            }
            else
            {
                id = null!;
                return false;
            }
        }


        public static GitId Parse(string idString)
        {
            if (TryParse(idString, out var v))
                return v;
            else
                throw new ArgumentOutOfRangeException(nameof(idString));
        }

        public static byte[] StringToByteArray(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                throw new ArgumentNullException(nameof(hex));

            int n = hex.Length / 2; // Note this trims an odd final hexdigit, if there is one
            byte[] bytes = new byte[n];

            for (int i = 0; i < n; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }


        public override int GetHashCode()
        {
            // Combination of First and some other should provide good hashing over subsets of hashes
            return BitConverter.ToInt32(_bytes, _offset) ^ BitConverter.ToInt32(_bytes, _offset + 16);
        }

        const string hexChars = "0123456789abcdef";
        public override string ToString()
        {
            int byteCount = HashLength(Type);
            var chars = new char[byteCount * 2];

            for (int i = 0; i < byteCount; i++)
            {
                var b = _bytes[_offset + i];
                chars[2 * i] = hexChars[b >> 4];
                chars[2 * i + 1] = hexChars[b & 0xF];
            }

            return new string(chars);
        }

        /// <summary>
        /// Maximum hash length currently supported. Currently 32
        /// </summary>
        public static readonly int MaxHashLength = 32;

        /// <summary>
        /// Return length of hash in bytes
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static int HashLength(GitIdType type)
            => type switch
            {
                GitIdType.Sha1 => 20,
                GitIdType.Sha256 => 32,
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };

        public int CompareTo(GitId? other)
        {
            if (other is null)
                return 1;

            int n = (int)Type - (int)other.Type;
            if (n != 0)
                return n;

            return HashCompare(other);
        }

        string IFormattable.ToString(string? format, IFormatProvider? formatProvider)
        {
            return ToString(format);
        }

        public string ToString(string? format)
        {
            if (string.IsNullOrEmpty(format) || format == "G")
                return ToString();

            if (format == "x")
                return ToString().Substring(0, 8);
            else if (format == "X")
                return ToString().Substring(0, 8).ToUpperInvariant();
#if !NETFRAMEWORK
            if (format!.StartsWith("x", StringComparison.Ordinal) && int.TryParse(format.AsSpan(1), out var xLen))
#else
            if (format!.StartsWith("x", StringComparison.Ordinal) && int.TryParse(format.Substring(1), out var xLen))
#endif
                return ToString().Substring(0, xLen);
#if !NETFRAMEWORK
            else if (format.StartsWith("X", StringComparison.Ordinal) && int.TryParse(format.AsSpan(1), out var xlen))
#else
            else if (format.StartsWith("X", StringComparison.Ordinal) && int.TryParse(format.Substring(1), out var xlen))
#endif
                return ToString().Substring(0, xlen).ToUpperInvariant();

            throw new ArgumentOutOfRangeException(nameof(format));
        }

        public static bool operator ==(GitId? one, GitId? other)
            => one?.Equals(other) ?? (other is null);

        public static bool operator !=(GitId? one, GitId? other)
            => !(one?.Equals(other) ?? (other is null));

        public byte this[int index]
        {
            get
            {
                if (index < 0 || index > HashLength(Type))
                    throw new ArgumentOutOfRangeException(nameof(index));

                return _bytes[index + _offset];
            }
        }

        public static bool operator <(GitId left, GitId right)
        {
            return (left is null) ? right is not null : left.CompareTo(right) < 0;
        }

        public static bool operator <=(GitId left, GitId right)
        {
            return (left is null) || left.CompareTo(right) <= 0;
        }

        public static bool operator >(GitId left, GitId right)
        {
            return left is not null && left.CompareTo(right) > 0;
        }

        public static bool operator >=(GitId left, GitId right)
        {
            return (left is null) ? (right is null) : left.CompareTo(right) >= 0;
        }
    }
}
