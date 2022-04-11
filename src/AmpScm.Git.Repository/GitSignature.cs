using System;
using System.Diagnostics;
using AmpScm.Buckets.Git;

namespace AmpScm.Git
{
    public sealed class GitSignature : IEquatable<GitSignature>
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string _value;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string? _email;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        DateTimeOffset _when;

        internal GitSignature(GitSignatureRecord signature)
        {
            _value = signature.Name;
            _email = signature.Email;
            _when = signature.When;
        }

        public GitSignature(string name, string email, DateTimeOffset now)
        {
            _value = name ?? throw new ArgumentNullException(nameof(name));
            _email = email ?? throw new ArgumentNullException(nameof(email));
            _when = now;
            if (email.IndexOfAny(new[] { '<', '>' }) >= 0)
                throw new ArgumentOutOfRangeException(email);
        }

        internal GitSignature(string authorValue)
        {
            _value = authorValue;
        }

        public string Name
        {
            get => MyRead()._value;
        }

        public string Email
        {
            get => MyRead()._email!;
        }

        public DateTimeOffset When
        {
            get => MyRead()._when;
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

        internal GitSignature MyRead()
        {
            if (_email != null)
                return this;

            int nS = _value.LastIndexOf('<');
            int nF = _value.IndexOf('>', nS + 1);

            _email = _value.Substring(nS + 1, nF - nS - 1);
            string[] time = _value.Substring(nF + 2).Split(new[] { ' ' }, 2);

            if (int.TryParse(time[0], out var unixtime) && int.TryParse(time[1], out var offset))
            {
                _when = DateTimeOffset.FromUnixTimeSeconds(unixtime).ToOffset(TimeSpan.FromMinutes((offset / 100) * 60 + (offset % 100)));
            }

            while (nS > 0 && char.IsWhiteSpace(_value, nS - 1))
                nS--;

            _value = _value.Substring(0, nS);

            return this;
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
