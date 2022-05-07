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
            options?.Verify();
            //var (_, txt) = await c.Repository.RunPorcelainCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

            await c.ThrowNotImplemented();
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

            if (options.Shared)
                args.Add("--shared");
            if (options.Sparse)
                args.Add("--sparse");

            if (options.Local == true)
                args.Add("--local");
            else if (options.Local == false)
                args.Add("--no-local");

            if (options.InitialConfiguration is not null)
            {
                foreach(var (k,v) in options.InitialConfiguration)
                {
                    args.Add("-c");
                    args.Add($"{k}={v}");
                }
            }

            args.Add("--");
            args.Add(sourcePath);
            args.Add(path);

            await c.Repository.RunPlumbingCommandOut("clone", args.ToArray());
        }
    }
}
