using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Porcelain
{
    public enum GitArchiveFormat
    {
        Zip,
        Tar,
        TarGz
    }
    public class GitArchiveArgs : GitPorcelainArgs
    {
        public GitArchiveFormat? Format { get; set; }
        public string[]? Paths { get; set; }
        public string? Prefix { get; set; }
        public bool WorktreeAttributes { get; set; }

        public override void Verify()
        {
            //throw new NotImplementedException();
        }
    }

    partial class GitPorcelain
    {
        [GitCommand("archive")]
        public static async ValueTask Archive(this GitPorcelainClient c, string? file, string treeIsh, GitArchiveArgs? options = null)
        {
            options?.Verify();
            options ??= new();

            List<string> args = new();

            if (options.Format.HasValue)
                switch (options.Format.Value)
                {
                    case GitArchiveFormat.Zip:
                        args.Add("--format=zip");
                        break;
                    case GitArchiveFormat.Tar:
                        args.Add("--format=tar");
                        break;
                    case GitArchiveFormat.TarGz:
                        args.Add("--format=tgz");
                        break;
                }

            if (!string.IsNullOrEmpty(file))
                args.Add($"--output={file.Replace(Path.DirectorySeparatorChar, '/')}");

            if (!string.IsNullOrEmpty(options.Prefix))
                args.Add($"--prefix={options.Prefix.Replace(Path.DirectorySeparatorChar, '/').TrimEnd('/') + '/'}");

            if (options.WorktreeAttributes)
                args.Add("--worktree-attributes");

            args.Add(treeIsh);

            if (options.Paths?.Any() ?? false)
                args.AddRange(options.Paths);

            await c.Repository.RunGitCommandAsync("archive", args);
        }
    }
}
