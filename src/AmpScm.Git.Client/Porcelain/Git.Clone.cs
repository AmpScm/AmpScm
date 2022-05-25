using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace AmpScm.Git.Client.Porcelain
{
    public class GitCloneArgs : GitPorcelainArgs
    {
        /// <summary>
        /// Adds alternate reference if cloning locally
        /// </summary>
        public bool Shared { get; set; }
        public bool Mirror { get; set; }
        public bool Bare { get; set; }
        public bool? Tags { get; set; }
        public bool NoCheckOut { get; set; }
        public bool? SingleBranch { get; set; }

        public string? OriginName { get; set; }
        public string? Branch { get; set; }
        public string? TemplatePath { get; set; }

        public int? Depth { get; set; }

        /// <summary>
        /// Bypasses git transport and just copies info if true, or explicitly uses git transport if false.
        /// </summary>
        public bool? Local { get; set; }

        public IEnumerable<(string, string)>? InitialConfiguration { get; set; }
        public bool Sparse { get; set; }

        public override void Verify()
        {
            //throw new NotImplementedException();
        }
    }

    partial class GitPorcelain
    {
        [GitCommand("clone")]
        public static async ValueTask Clone(this GitPorcelainClient c, Uri sourceUri, string path, GitCloneArgs? options = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            else if (sourceUri is null)
                throw new ArgumentNullException(nameof(sourceUri));
            else if (!sourceUri.IsAbsoluteUri)
                throw new ArgumentOutOfRangeException(nameof(sourceUri));
            else if (sourceUri.Scheme == "file")
            {
                await Clone(c, sourceUri.AbsolutePath, path, options);
                return;
            }

            options ??= new();
            options.Verify();

            List<string> args = new();
            PrepareCloneArgs(options, args);
            args.Add(sourceUri.AbsoluteUri);
            args.Add(path);

            await c.Repository.RunGitCommandAsync("clone", args);

            RemoveReadOnlyIfNecessary(c.Repository);
        }

        [GitCommand("clone")]
        public static async ValueTask Clone(this GitPorcelainClient c, string sourcePath, string path, GitCloneArgs? options = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            else if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentNullException(nameof(sourcePath));
            else if (Uri.TryCreate(sourcePath, UriKind.Absolute, out var uri) && uri.Scheme != "file")
            {
                await Clone(c, uri, path, options);
                return;
            }
            else if (!Directory.Exists(sourcePath))
                throw new DirectoryNotFoundException($"Git Directory '{sourcePath}' not found");

            options ??= new();
            options.Verify();

            List<string> args = new();
            PrepareCloneArgs(options, args);
            args.Add(sourcePath);
            args.Add(path);

            await c.Repository.RunGitCommandAsync("clone", args);

            RemoveReadOnlyIfNecessary(c.Repository);
        }

        private static void PrepareCloneArgs(GitCloneArgs options, List<string> args)
        {
            if (options.Shared)
                args.Add("--shared");
            if (options.Sparse)
                args.Add("--sparse");

            if (options.Mirror)
                args.Add("--mirror"); // Implies --bare
            else if (options.Bare)
                args.Add("--bare");

            if (options.Local == true)
                args.Add("--local");
            else if (options.Local == false)
                args.Add("--no-local");

            if (options.SingleBranch == true)
                args.Add("--single-branch");
            else if (options.SingleBranch == false)
                args.Add("--no-single-branch");

            if (options.Tags == false)
                args.Add("--no-tags");

            if (options.NoCheckOut)
                args.Add("--no-checkout");

            if (options.Depth >= 1)
            {
                args.Add("--depth");
                args.Add(Convert.ToString(options.Depth.Value));
            }

            if (!string.IsNullOrEmpty(options.OriginName))
            {
                args.Add("--origin");
                args.Add(options.OriginName!);
            }

            if (!string.IsNullOrEmpty(options.Branch))
            {
                args.Add("--branch");
                args.Add(options.Branch!);
            }

            if (!string.IsNullOrEmpty(options.TemplatePath))
            {
                args.Add("--template");
                args.Add(options.TemplatePath!);
            }

            if (options.InitialConfiguration is not null)
            {
                foreach (var (k, v) in options.InitialConfiguration)
                {
                    args.Add("-c");
                    args.Add($"{k}={v}");
                }
            }

            args.Add("--");
        }
    }
}
