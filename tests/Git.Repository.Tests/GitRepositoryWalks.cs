using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AmpScm.Git;
using AmpScm.Git.Client.Plumbing;
using AmpScm.Git.References;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests
{
    [TestClass]
    public class GitRepositoryWalks
    {
        public TestContext TestContext { get; set; } = null!;

        static List<string> TestRepositories { get; } = GetTestRepositories().ToList();

        static IEnumerable<string> GetTestRepositories()
        {
            string p = Path.GetDirectoryName(typeof(GitRepositoryWalks).Assembly.Location)!;

            do
            {
                if (p != Path.GetPathRoot(p) && Directory.Exists(Path.Combine(p, ".git")))
                    yield return p;
                else if (Directory.Exists(Path.Combine(p, "git-testrepos")))
                {
                    foreach (var d in Directory.GetDirectories(Path.Combine(p, "git-testrepos"), "*-*"))
                    {
                        yield return d;
                    }
                }
            }
            while ((p = Path.GetDirectoryName(p)!) != null);
        }

        public static IEnumerable<object[]> TestRepositoryArgs => TestRepositories.Select(x => new object[] { x });


        [TestMethod]
        [DynamicData(nameof(TestRepositoryArgs))]
        public async Task CanOpenRepository(string path)
        {
            using var repo = GitRepository.Open(path);
            TestContext.WriteLine($"Looking at {repo}");
            TestContext.Write($" from {repo.Remotes["origin"]?.Url}");

            if (repo.IsBare)
                TestContext.Write($" [bare]");
            if (repo.IsLazy)
                TestContext.Write($" [lazy-loaded]");
            TestContext.WriteLine();

            Assert.IsTrue(repo.Remotes.Any(), "Has remotes");
            Assert.IsTrue(repo.Remotes.Any(x => x.Name == "origin"), "Has origin remote");

            Assert.IsTrue(repo.Commits.Any(), "Has commits");
            if (!repo.IsLazy)
            {
                Assert.IsTrue(repo.Trees.Any(), "Has trees");
                Assert.IsTrue(repo.Blobs.Any(), "Has blobs");
                //Assert.IsTrue(repo.Tags.Any(), "Has tags");
            }

            Assert.IsNotNull(repo.Head, "Repository has an HEAD");
            Assert.IsTrue(repo.Head is GitSymbolicReference, "HEAD is an Symbolic reference");
            Assert.IsNotNull(repo.Head?.Commit, "Head can be resolved");
            TestContext.WriteLine($"Last change: {repo.Head.Commit.Author}");

            await foreach (var r in repo.References)
            {
                TestContext.WriteLine($"{r.Name} {r.ShortName.PadRight(15)} - {r.Commit?.Id:x7} - {r.Commit?.Author}");
            }

            if (!repo.IsShallow)
                Assert.IsNotNull(repo.Commits.FirstOrDefault(x => x.Parents.Count > 1), "Repository has merges");

            Assert.IsTrue(repo.Branches.Any());
        }

        [TestMethod]
        [DynamicData(nameof(TestRepositoryArgs))]
        public async Task WalkHistory(string path)
        {
            using var repo = GitRepository.Open(path);

            if (repo.IsShallow)
                return;

            var r = await repo.GetPlumbing().RevisionList(new GitRevisionListArgs { MaxCount = 32, FirstParentOnly = true }).ToListAsync();

            Assert.AreEqual(32, r.Count);
            Assert.AreEqual(32, r.Count(x => x != null));
            Assert.AreEqual(32, r.Distinct().Count());

            var revs = repo.Head.Revisions.Take(32).Select(x => x.Commit.Id).ToList();

            if (!r.SequenceEqual(revs))
            {
                int? nDiff = null;
                for (int i = 0; i < Math.Min(revs.Count, r.Count); i++)
                {
                    TestContext.WriteLine($"{i:00} {r[i]} - {revs[i]}");

                    if (!nDiff.HasValue && r[i] != revs[i])
                        nDiff = i;
                }
                Assert.Fail($"Different list at {nDiff}");
            }


            if (repo.Commits[GitId.Parse("b71c6c3b64bc002731bc2d6c49080a4855d2c169")] is GitCommit manyParents)
            {
                TestContext.WriteLine($"Found commit {manyParents}, so we triggered the many parent handling");
                manyParents.Revisions.Take(3).ToList();

                Assert.IsTrue(manyParents.Parent!.ParentCount > 3);
            }

            var id = repo.Head.Id!.ToString();

            for (int i = id.Length - 1; i > 7; i--)
            {
                string searchVia = id.Substring(0, i);
                Assert.IsNotNull(await repo.Commits.ResolveIdAsync(searchVia), $"Able to find via {searchVia}, len={i}");
            }
        }


        public static IEnumerable<object[]> TestRepositoryArgsBitmapAndRev => TestRepositoryArgs.Where(x => x[0] is string s && Directory.GetFiles(Path.Combine(s, ".git", "objects", "pack"), "*.bitmap").Any()).Concat(new[] { "1", "2" }.Select(x => new[] { ">" + x }));
        [TestMethod]
        [DynamicData(nameof(TestRepositoryArgsBitmapAndRev))]
        public async Task WalkObjectsViaBitmap(string path)
        {
            if (path.Contains('>'))
            {
                var pp = path.Substring(1);
                GitRepository gc = GitRepository.Open(typeof(GitRepositoryWalks).Assembly.Location);
                path = TestContext.PerTestDirectory(pp);

                await gc.GetPlumbing().RunRawCommand("clone", new[] { "--bare", gc.FullPath, path });

                gc = GitRepository.Open(path);
                Assert.AreEqual(path, gc.FullPath);

                if (pp == "1")
                    await gc.GetPlumbing().Repack(new GitRepackArgs { WriteBitmap = true, SinglePack = true });
                else
                    await gc.GetPlumbing().Repack(new GitRepackArgs { WriteBitmap = true, SinglePack = true, WriteMultiPack = true });
            }
            using var repo = await GitRepository.OpenAsync(path);

            Assert.IsTrue(repo.Commits.Count() > 0);
            Assert.IsTrue(repo.Trees.Count() > 0);
            Assert.IsTrue(repo.Blobs.Count() > 0);
            Assert.IsTrue(repo.TagObjects.Count() > 0);
        }

        [TestMethod]
        public async Task WalkWorkTreeWorkingCopy()
        {
            var path = TestContext.PerTestDirectory();
            var path2 = TestContext.PerTestDirectory("2");
            {
                using GitRepository gc = GitRepository.Open(typeof(GitRepositoryWalks).Assembly.Location);
                await gc.GetPlumbing().RunRawCommand("clone", new[] { gc.FullPath, path2 });
            }
            {
                using GitRepository gc = GitRepository.Open(path2);
                Assert.AreEqual(path2, gc.FullPath);
                await gc.GetPlumbing().RunRawCommand("worktree", new[] { "add", "-b", "MyWorkTree", path });
            }

            using var repo = GitRepository.Open(path);
            Assert.AreEqual(path, repo.FullPath);

            Assert.IsTrue(repo.Commits.Any());
            Assert.IsTrue(repo.References.Any());
            Assert.AreEqual("refs/heads/MyWorkTree", repo.Head.Resolved.Name);
        }
    }
}
