using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;

namespace AmpScm.Git.Client.Porcelain
{
    public class GitFastExportArgs : GitPorcelainArgs
    {
        public bool All { get; set; }
        public override void Verify()
        {
            //throw new NotImplementedException();
        }
    }

    partial class GitPorcelain
    {
        [GitCommand("fast-export")]
        public static async ValueTask<Bucket> FastExport(this GitPorcelainClient c, GitFastExportArgs? options = null)
        {
            options?.Verify();
            options ??= new();

            var args = new List<string>();

            if (options.All)
                args.Add("--all");

            return await c.Repository.RunGitCommandBucketAsync("fast-export", args);
        }
    }
}
