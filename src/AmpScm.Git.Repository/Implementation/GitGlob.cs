using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AmpScm.Git.Repository.Implementation;

internal enum GitGlobFlags
{
    None = 0,
    ParentPath = 1,
    CaseInsensitive = 2,
}
internal class GitGlob
{
#pragma warning disable MA0051 // Method is too long
    internal static bool Match(string pattern, string path, GitGlobFlags flags)
#pragma warning restore MA0051 // Method is too long
    {
        RegexOptions ro = RegexOptions.CultureInvariant;

        if ((flags & GitGlobFlags.CaseInsensitive) != 0)
            ro |= RegexOptions.IgnoreCase;

        StringBuilder sb = new StringBuilder("^");

        for (int i = 0; i < pattern.Length; i++)
        {
            switch (pattern[i])
            {
                case '*' when (i + 1 < pattern.Length && pattern[i + 1] == '*'):
                    if (i > 0 && i + 2 == pattern.Length && pattern[i - 1] == '/')
                    {
                        sb.Insert(sb.Length - "[/\\\\]".Length, '(');
                        sb.Append(".*)?");
                    }
                    else
                        sb.Append(".*");
                    i++;
                    break;
                case '*':
                    sb.Append("[^/\\\\]*");
                    break;
                case '?':
                    sb.Append("[^/\\\\]");
                    break;
                case '\\':
                    if (i + 1 < pattern.Length)
                        sb.Append(Regex.Escape(pattern[++i].ToString()));
                    break;
                case '/':
                    sb.Append("[/\\\\]");
                    break;
                case '[':
                    int n = pattern.IndexOf(']', i);
                    if (n >= 0)
                    {
                        sb.Append('[');
                        while (i < n)
                        {
                            sb.Append(Regex.Escape(pattern[i++].ToString()));
                        }
                        sb.Append(']');
                        i = n;
                    }
                    else
                        sb.Append("\\[");
                    break;
                default:
                    if ("\\[](){}<>^$".Contains(pattern[i], StringComparison.Ordinal))
                        sb.Append(Regex.Escape(pattern[i].ToString()));
                    else
                        sb.Append(pattern[i]);
                    break;
            }
        }


        if ((flags & GitGlobFlags.ParentPath) == 0)
        {
            sb.Append('$');
        }

#pragma warning disable MA0009 // Add regex evaluation timeout
        return Regex.IsMatch(path.Replace(Path.DirectorySeparatorChar, '/'), sb.ToString(), ro);
#pragma warning restore MA0009 // Add regex evaluation timeout
    }
}
