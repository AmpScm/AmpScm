using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Porcelain
{
    public class GitInitArgs : GitPorcelainArgs
    {
        /// <summary>
        /// Adds alternate reference if cloning locally
        /// </summary>
        public bool Bare { get; set; }
        public string? Branch { get; set; }
        public string? TemplatePath { get; set; }

        public override void Verify()
        {
            //throw new NotImplementedException();
        }
    }

    partial class GitPorcelain
    {
        [GitCommand("init")]
        public static async ValueTask Init(this GitPorcelainClient c, string path, GitInitArgs? options = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));


            options ??= new();
            options.Verify();

            List<string> args = new();

            if (options.Bare)
                args.Add("--bare");

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


            args.Add(path);

            await c.Repository.RunPlumbingCommand("init", args.ToArray());

        }
    }
}
