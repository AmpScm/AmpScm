﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{

    public enum GitMultiPackIndexCommand
    {
        Write,
        Verify,
        Repack,
    }
    public class GitMultiPackIndexArgs : GitPlumbingArgs
    {
        public GitMultiPackIndexCommand Command { get; set; }

        public bool? Bitmap { get; set; }
        public override void Verify()
        {

        }
    }

    partial class GitPlumbing
    {
        [GitCommand("multi-pack-index")]
        public static async ValueTask MultiPackIndex(this GitPlumbingClient c, GitMultiPackIndexArgs a)
        {
            a.Verify();

            List<string> args = new();

            args.Add(a.Command switch
            {
                GitMultiPackIndexCommand.Write => "write",
                GitMultiPackIndexCommand.Verify => "verify",
                GitMultiPackIndexCommand.Repack => "repack",
                _ => throw new ArgumentException()
            });

            // Bitmap is nullable. Handle the two explicit states
            if (a.Bitmap == true)
                args.Add("--bitmap");
            else if (a.Bitmap == false)
                args.Add("--no-bitmap");

            await c.Repository.RunPlumbingCommandOut("multi-pack-index", args.ToArray());
        }
    }
}
