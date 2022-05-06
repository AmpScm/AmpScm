using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public class GitGCArgs : GitPlumbingArgs
    {
        public DateTime? PruneDate { get; set; }
        public bool Aggressive { get; set; }
        public bool Auto { get; set; }
        public bool Force { get; set; }
        public bool KeepLargestPack { get; set; }
        public override void Verify()
        {
            //throw new NotImplementedException();
        }
    }

    partial class GitPlumbing
    {
        [GitCommand("gc")]
        public static async ValueTask GC(this GitPlumbingClient c, GitGCArgs a)
        {
            a.Verify();

            List<string> args = new List<string>();

            if (a.Aggressive)
                args.Add("--aggressive");
            if (a.Auto)
                args.Add("--auto");
            if (a.Force)
                args.Add("--force");
            if (a.KeepLargestPack)
                args.Add("--keep-largest-pack");
            if (a.PruneDate.HasValue)
                args.Add($"--prune={a.PruneDate.Value.Date.ToString("yyyy-MM-dd")}");

            await c.Repository.RunPlumbingCommandOut("gc", args.ToArray());
        }
    }
}
