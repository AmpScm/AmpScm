using System;
using System.Diagnostics;
using AmpScm.Buckets.Git;

namespace AmpScm.Git
{
    public sealed class GitSignature : IEquatable<GitSignature>
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _name;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string? _email;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DateTimeOffset _when;

        internal GitSignature(GitSignatureRecord signature)
        {
            _name = signature.Name;
            _email = signature.Email;
            _when = signature.When;
        }

        private static readonly char[] _ltgt = new[] { '<', '>' };

        public GitSignature(string name, string email, DateTimeOffset now)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _email = email ?? throw new ArgumentNullException(nameof(email));
            _when = now;
            if (email.IndexOfAny(_ltgt) >= 0)
                throw new ArgumentOutOfRangeException(email);
        }

        internal GitSignature(string authorValue)
        {
            _name = authorValue;
        }

        public string Name
        {
            get => _name;
        }

        public string Email
        {
            get => _email!;
        }

        public DateTimeOffset When
        {
            get => _when;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as GitSignature);
        }

        public bool Equals(GitSignature? other)
        {
            if (other is null)
                return false;

            return Name == other.Name && Email == other.Email && When == other.When;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode(StringComparison.Ordinal) ^ When.GetHashCode();
        }

        public override string ToString()
        {
            return $"{Name} <{Email}> {When}";
        }

        internal GitSignatureRecord AsRecord()
        {
            return new GitSignatureRecord
            {
                Name = Name,
                Email = Email,
                When = When
            };
        }
    }
}
