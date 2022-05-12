using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public class GitIndexPackArgs : GitPlumbingArgs
    {
        public bool? ReverseIndex { get; set; }
        public bool FixThin { get; set; }

        public override void Verify()
        {
        }
    }

    partial class GitPlumbing
    {
        [GitCommand("index-pack")]
        public static async ValueTask IndexPack(this GitPlumbingClient c, string path, GitIndexPackArgs? options = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            options ??= new GitIndexPackArgs();
            options.Verify();

            List<string> args = new();

            if (options.FixThin)
                args.Add("--fix-thin");

            if (options.ReverseIndex == true)
                args.Add("--rev-index");
            else if (options.ReverseIndex == false)
                args.Add("--no-rev-index");

            args.Add(path);

#if NET5_0_OR_GREATER
            if (OperatingSystem.IsWindows())
#endif
            {
                string idx = Path.ChangeExtension(path, ".idx");
                if (File.Exists(idx))
                {
                    File.SetAttributes(idx, FileAttributes.Normal);
                    File.Delete(idx);
                }
            }

            await c.Repository.RunGitCommandAsync("index-pack", args);
        }
    }
}
