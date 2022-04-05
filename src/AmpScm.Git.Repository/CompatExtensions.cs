using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git
{
    internal static class CompatExtensions
    {
#if NETFRAMEWORK
        public static string Replace(this string? on, string oldValue, string newValue, StringComparison comparison)
        {
            if (on is null)
                throw new ArgumentNullException(nameof(on));
            if (comparison != StringComparison.Ordinal)
                throw new ArgumentOutOfRangeException(nameof(comparison));
            return on.Replace(oldValue, newValue);
        }

        public static int IndexOf(this string on, char value, StringComparison comparison)
        {
            if (comparison != StringComparison.Ordinal)
                throw new ArgumentOutOfRangeException(nameof(comparison));

            return on.IndexOf(value);
        }

        public static bool Contains(this string on, char value, StringComparison comparison)
        {
            if (comparison != StringComparison.Ordinal)
                throw new ArgumentOutOfRangeException(nameof(comparison));

            return on.Contains(value);
        }

        public static bool Contains(this string on, string value, StringComparison comparison)
        {
            if (comparison != StringComparison.Ordinal)
                throw new ArgumentOutOfRangeException(nameof(comparison));

            return on.Contains(value);
        }

        public static int GetHashCode(this string on, StringComparison comparison)
        {
            if (comparison != StringComparison.Ordinal)
                throw new ArgumentOutOfRangeException(nameof(comparison));

            return on.GetHashCode();
        }
#endif
    }
}
