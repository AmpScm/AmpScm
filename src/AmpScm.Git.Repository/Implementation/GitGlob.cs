using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AmpScm.Git.Repository.Implementation
{
    internal enum GitGlobFlags
    {
        None = 0,
        ParentPath = 1,
        CaseInsensitive = 2,
    }
    internal class GitGlob
    {
        internal static bool Match(string pattern, string path, GitGlobFlags flags)
        {
            RegexOptions ro = RegexOptions.CultureInvariant;

            if ((flags & GitGlobFlags.CaseInsensitive) != 0)
                ro |= RegexOptions.IgnoreCase;

            StringBuilder sb = new StringBuilder("^");

            for(int i = 0; i < pattern.Length; i++)
            {
                switch(pattern[i])
                {
                    case '*':
                        if (i + 1 < pattern.Length && pattern[i + 1] == '*')
                        {
                            sb.Append(".*");
                            i++;
                        }
                        else
                            sb.Append("[^/\\\\]*");
                        break;
                    case '?':
                        sb.Append("[^/\\\\]");
                        break;
                    case '/':
                    case '\\':
                        sb.Append("[/\\\\]");
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
                sb.Append('$');

            return Regex.IsMatch(path, sb.ToString(), ro);
        }
    }
}
