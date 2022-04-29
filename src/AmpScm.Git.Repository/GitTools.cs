using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git
{
    public static class GitTools
    {
        public static string GetNormalizedFullPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            path = Path.GetFullPath(path);

            string? dir = Path.GetDirectoryName(path)!;

            if (File.Exists(path))
            {
                path = Directory.EnumerateFiles(dir, Path.GetFileName(path)).Single();
            }
            else if (Directory.Exists(path))
            {
                path = Directory.EnumerateDirectories(dir, Path.GetFileName(path)).Single();
            }

            for(string parent = Path.GetDirectoryName(dir)!; parent != null && parent != dir; parent = Path.GetDirectoryName(parent)!)
            {
                var p = Directory.GetDirectories(parent!, Path.GetFileName(dir)).FirstOrDefault();

                if (p != null && (!path.StartsWith(p, StringComparison.Ordinal) || path[p.Length] == Path.DirectorySeparatorChar))
                {
#if !NETFRAMEWORK
                    path = string.Concat(p, path.AsSpan(dir.Length));
#else
                    path = p + path.Substring(dir.Length);
#endif
                }
            }
            while (dir != (dir = Path.GetDirectoryName(dir)));

            if (path.Length > 2 && path[1] == ':' && char.IsLower(path, 0))
                path = char.ToUpperInvariant(path[0]) + path.Substring(1);

            return path;
        }

        public static string? FirstLine(string? message)
        {
            if (message == null)
                return null;

            return message.Split(new char[] { '\n' }, 2)[0].Trim();
        }

        internal static string? CreateSummary(string? message)
        {
            if (message == null)
                return null;

            if (message.Contains('\r', StringComparison.Ordinal))
                message = message.Replace("\r", "", StringComparison.Ordinal);

            var lines = message.Split(new char[] { '\n' }, 4);

            int st;
            for(st = 0; st < lines.Length; st++)
            {
                if (string.IsNullOrWhiteSpace(lines[st]))
                    break;
            }

            return  string.Join("\n", lines.Take(st));
        }
    }
}
