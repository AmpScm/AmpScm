using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm;
using AmpScm.Git;
using AmpScm.Git.Client.Plumbing;
using AmpScm.Git.Client.Porcelain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests;

public static class TestExtensions
{
    public static async ValueTask<string> CreateCloneAsync(this TestContext self, GitTestDir testDir, bool shareOdb = true)
    {
        return await CreateCloneAsync(self, GitTestEnvironment.GetRepository(testDir), shareOdb);
    }

    public static async ValueTask<string> CreateCloneAsync(this TestContext self, string? repos = null, bool shareOdb = true)
    {
        using var repo = GitRepository.Open(repos ?? GitTestEnvironment.GetRepository(GitTestDir.Default));

        var dir = self.PerTestDirectory("repo");

        await repo.GetPorcelain().Clone(repo.FullPath, dir, new()
        {
            Shared = shareOdb,
            InitialConfiguration = new[] { ("user.email", "me@myself.and.i"), ("user.name", "Me") }
        });

        return dir;
    }
}
