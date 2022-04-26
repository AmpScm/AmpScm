using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm;
using AmpScm.Git;
using AmpScm.Git.Client.Plumbing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests
{
    public static class TestExtensions
    {
        public static async ValueTask<string> CreateCloneAsync(this TestContext self, string? repos = null, bool shareOdb=true)
        {
            repos ??= Path.GetDirectoryName(typeof(TestExtensions).Assembly.Location) ?? throw new ArgumentNullException(nameof(repos));

            using var r = GitRepository.Open(repos);

            var dir = self.PerTestDirectory("repo");

            List<string> args = new List<string>() { r.FullPath, dir };
            if (shareOdb)
                args.Add("-s");

            await r.GetPlumbing().RunRawCommand("clone", args.ToArray());

            return dir;
        }
    }
}
