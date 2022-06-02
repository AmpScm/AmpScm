using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Diff
{
    public abstract class DiffTokenizer<TToken> : EqualityComparer<TToken>
        where TToken : notnull
    {
        public override abstract bool Equals(TToken? x, TToken? y);
        public override abstract int GetHashCode([DisallowNull] TToken obj);
    }

    public class StringTokenizer : DiffTokenizer<string>
    {
        Dictionary<string, string> _hash;
        IEqualityComparer<string>? _comparer;


        public bool IgnoreCase { get; init; }

        public StringTokenizer()
        {
            _hash = new();
            //_comparer = EqualityComparer<string>.Default;
        }

        public override bool Equals(string? x, string? y)
        {
            _comparer ??= SetupComparer();

            return _comparer.Equals(x, y);
        }

        private IEqualityComparer<string>? SetupComparer()
        {
            return EqualityComparer<string>.Default;
        }

        public override int GetHashCode([DisallowNull] string obj)
        {
            _comparer ??= SetupComparer();

            return _comparer.GetHashCode(obj);
        }

    }
}
