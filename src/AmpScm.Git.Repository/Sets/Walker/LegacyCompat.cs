namespace AmpScm.Git.Sets.Walker;

internal static class LegacyCompat
{
#if NETFRAMEWORK
    public static bool TryGetValue<T>(this HashSet<T> set, T value, /* [MaybeNullWhen(false)] */ out T existingValue)
        where T : class, IEquatable<T>
    {
        if (set.Contains(value))
            foreach (var v in set)
            {
                if (v.Equals(value))
                {
                    existingValue = v;
                    return true;
                }
            }
        existingValue = default!;
        return false;
    }

    public static bool TryPeek<T>(this Stack<T> stack, /* [MaybeNullWhen(false)] */ out T value)
    {
        if (stack.Count > 0)
        {
            value = stack.Peek();
            return true;
        }

        value = default!;
        return false;
    }
#endif
}
