namespace AmpScm.Git;

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

    internal static int GetHashCode(this string on, StringComparison comparison)
        => comparison switch {
            StringComparison.Ordinal => StringComparer.Ordinal.GetHashCode(on),
            StringComparison.OrdinalIgnoreCase => StringComparer.OrdinalIgnoreCase.GetHashCode(on),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison))
        };
#endif
}
