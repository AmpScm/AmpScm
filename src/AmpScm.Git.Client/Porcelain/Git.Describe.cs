﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Porcelain
{
    public class GitDescribeArgs : GitPorcelainArgs
    {
        public override void Verify()
        {
            throw new NotImplementedException();
        }
    }

    partial class GitPorcelain
    {
        [GitCommand("describe")]
        public static async ValueTask Describe(this GitPorcelainClient c, GitDescribeArgs? options = null)
        {
            options?.Verify();
            //var (_, txt) = await c.Repository.RunPorcelainCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

            await c.ThrowNotImplemented();
        }
    }
}
