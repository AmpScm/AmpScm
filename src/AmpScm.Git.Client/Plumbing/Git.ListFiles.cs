using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public class GitListFilesArgs : GitPlumbingArgs
    {
        public override void Verify()
        {
            throw new NotImplementedException();
        }
    }

    public partial class GitPlumbing
    {
        [GitCommand("ls-files")]
        public static async ValueTask<string> ListFiles(this GitPlumbingClient c, GitListFilesArgs options)
        {
            options.Verify();
            var (_, txt) = await c.Repository.RunGitCommandOutAsync("ls-files", new string[] { });

            return txt;
        }
    }
}
