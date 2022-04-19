using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public class GitRepackArgs : GitPlumbingArgs
    {
        public bool SinglePack { get; set; }
        public bool UnreachableAsLoose { get; set; }
        public bool RemoveUnused { get; set; }
        public bool Quiet { get; set; }
        public bool WriteBitmap { get; set; }
        public bool WriteMultiPack { get; set; }

        public override void Verify()
        {
            //throw new NotImplementedException();
        }
    }

    partial class GitPlumbing
    {
        [GitCommand("repack")]
        public static async ValueTask Repack(this GitPlumbingClient c, GitRepackArgs a)
        {
            a.Verify();
            var args = new List<string>();

            if (a.SinglePack)
            {
                if (a.UnreachableAsLoose)
                    args.Add("-A");
                else
                    args.Add("-a");
            }
            if (a.RemoveUnused)
                args.Add("-d");
            if (a.Quiet)
                args.Add("-q");
            if (a.WriteBitmap)
                args.Add("--write-bitmap-index");
            if (a.WriteMultiPack)
                args.Add("--write-midx");


            var (_, txt) = await c.Repository.RunPlumbingCommandOut("repack", args.ToArray());
        }
    }
}
