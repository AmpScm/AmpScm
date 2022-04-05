using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Git.Repository;

namespace AmpScm.Git
{

    public partial class GitRepository
    {
        public static GitRepository Open(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            var (root, type) = FindGitRoot(path);

            if (root is null)
                throw new GitRepositoryException($"Git repository not found at '{path}'");

            return new GitRepository(root, type);
        }

        public static ValueTask<GitRepository> OpenAsync(string path, CancellationToken cancellation = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            var g = GitRepository.Open(path);
#pragma warning restore CA2000 // Dispose objects before losing scope

            return new ValueTask<GitRepository>(g);
        }

        /// <summary>
        /// Find git repository for <paramref name="path"/>. Start by looking at 'path/.git' (directory/file), then checking 
        /// if path is a (bare-)repository itself. Then start looking up for a '.git' in any parent directory. And if that
        /// fails, look for parent directories that are a repository.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        static (string? Path, GitRootType Type) FindGitRoot(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            path = Path.GetFullPath(path);
            string? p = path;

            while (p?.Length > 0)
            {
                var tst = Path.Combine(p, ".git");

                if (Directory.Exists(tst) && File.Exists(Path.Combine(tst, "config")))
                    return (p, GitRootType.Normal);
                else if (File.Exists(tst) && TryReadRefFile(tst, "gitdir: ", out var v)
                    && Directory.Exists(v) && File.Exists(Path.Combine(v, "gitdir")))
                {
                    return (p, GitRootType.WorkTree);
                }

                if (ReferenceEquals(p, path))
                {
                    if (!Directory.Exists(Path.Combine(path, ".git"))
                          && File.Exists(Path.Combine(path, "config"))
                          && File.Exists(Path.Combine(path, "HEAD"))
                          && Directory.Exists(Path.Combine(path, "refs")))
                    {
                        return (path, path.EndsWith(Path.DirectorySeparatorChar + ".git", StringComparison.OrdinalIgnoreCase)
                                        ? GitRootType.None : GitRootType.Bare);
                    }
                }

                p = Path.GetDirectoryName(p);
            }

            p = path;

            while (p?.Length > 0)
            {
                if (File.Exists(Path.Combine(p, "config"))
                    && File.Exists(Path.Combine(p, "HEAD"))
                    && Directory.Exists(Path.Combine(p, "refs")))
                {
                    return (path, GitRootType.Bare);
                }

                p = Path.GetDirectoryName(p);
            }

            return (null, GitRootType.None);
        }

        internal static bool TryReadRefFile(string path, string? prefix, [NotNullWhen(true)] out string? result)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                using var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 512);
                byte[] buf = new byte[512];

                int n = f.Read(buf, 0, buf.Length);

                if (buf.Length > 0 && n < buf.Length && n >= (prefix?.Length ?? 0))
                {
                    BucketEol eol = BucketEol.None;
                    if (buf.Length > 2 && buf[buf.Length - 1] == '\n')
                    {
                        if (buf[buf.Length - 2] == '\r')
                            eol = BucketEol.CRLF;
                        else
                            eol = BucketEol.LF;
                    }
                    else if (buf[buf.Length - 1] == '\r')
                        eol = BucketEol.CR;
                    else if (buf[buf.Length - 1] == '\n')
                        eol = BucketEol.LF;
                    else if (buf[buf.Length - 1] == '\0')
                        eol = BucketEol.Zero;

                    BucketBytes bb = new BucketBytes(buf, 0, n);

                    if (prefix != null)
                    {
                        var p = bb.Slice(0, prefix.Length).ToUTF8String();

                        if (prefix != p)
                        {
                            result = null;
                            return false;
                        }
                        bb = bb.Slice(p.Length);
                    }

                    result = bb.ToUTF8String(eol);
                    return true;
                }
            }
            catch (IOException)
            { }
            catch (NotSupportedException)
            { }
            catch (SystemException)
            { }
#pragma warning restore CA1031 // Do not catch general exception types

            result = null;
            return false;
        }
    }
}
