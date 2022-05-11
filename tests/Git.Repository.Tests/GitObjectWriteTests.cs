using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AmpScm;
using AmpScm.Buckets;
using AmpScm.Git;
using AmpScm.Git.Client.Plumbing;
using AmpScm.Git.Client.Porcelain;
using AmpScm.Git.Objects;
using AmpScm.Git.References;
using AmpScm.Git.Repository;
using AmpScm.Git.Sets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests
{
    [TestClass]
    public class GitObjectWriteTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public async Task CreateRawObjects()
        {
            using var repo = GitRepository.Init(TestContext.PerTestDirectory());

            GitBlobWriter bw = GitBlobWriter.CreateFrom(Bucket.Create.FromUTF8("This is 'iota'."));

            var blob = await bw.WriteAndFetchAsync(repo);

            Assert.IsNotNull(blob);
            Assert.AreEqual("ce5beb5e8714fe6d04096988a48589e8312451a8", blob.Id.ToString());


            GitTreeWriter tw = GitTreeWriter.CreateEmpty();

            tw.Add("iota", blob);

            var treeId = await tw.WriteToAsync(repo);

            Assert.AreEqual("f6315a2112111a87d565eef0175d25ed01c7da6e", treeId.ToString());

            var fsckOutput = await repo.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
            Assert.AreEqual($"dangling tree {treeId}", fsckOutput);
        }

        [TestMethod]
        public async Task CreateSvnTree()
        {
            using var repo = GitRepository.Init(TestContext.PerTestDirectory());

            var items = new RepoItem[]
            {
                new RepoItem { Name = "iota", Content="This is dthe file 'iota'.\n" },
                new RepoItem { Name = "A" },
                new RepoItem { Name = "A/mu", Content="This is the file 'mu'.\n" },
                new RepoItem { Name = "A/B" },
                new RepoItem { Name = "A/B/lambda", Content="This is the file 'lambda'.\n"},
                new RepoItem { Name = "A/B/E", },
                new RepoItem { Name = "A/B/E/alpha", Content="This is the file 'alpha'.\n"},
                new RepoItem { Name = "A/B/E/beta", Content="This is the file 'beta'.\n" },
                new RepoItem { Name = "A/B/F" },
                new RepoItem { Name = "A/C" },
                new RepoItem { Name = "A/D" },
                new RepoItem { Name = "A/D/gamma", Content="This is the file 'gamma'.\n" },
                new RepoItem { Name = "A/D/G" },
                new RepoItem { Name = "A/D/G/pi", Content="This is the file 'pi'.\n" },
                new RepoItem { Name = "A/D/G/rho", Content="This is the file 'rho'.\n" },
                new RepoItem { Name = "A/D/G/tau", Content="This is the file 'tau'.\n" },
                new RepoItem { Name = "A/D/H" },
                new RepoItem { Name = "A/D/H/chi", Content = "This is the file 'chi'.\n" },
                new RepoItem { Name = "A/D/H/psi", Content = "This is the file 'psi'.\n" },
                new RepoItem { Name = "A/D/H/omega", Content = "This is the file 'omega'.\n" }
            };

            Dictionary<string, GitObject> ht = new();
            foreach (var i in items.Where(x => x.Content != null))
            {
                GitBlobWriter b = GitBlobWriter.CreateFrom(Bucket.Create.FromUTF8(i.Content!));

                var r = await b.WriteAndFetchAsync(repo);
                ht[i.Name] = r;
            }

            foreach (var m in items.Where(x => x.Content == null).OrderByDescending(x => x.Name).Concat(new[] { new RepoItem { Name = "" } }))
            {
                GitTreeWriter t = GitTreeWriter.CreateEmpty();

                foreach (var o in items.Where(x => x.Name.StartsWith(m.Name + "/") || (m.Name == "" && !x.Name.Contains('/'))).Select(x => new { Item = x, Name = x.Name.Substring(m.Name.Length).TrimStart('/') }).Where(x => !x.Name.Contains('/')))
                {
                    if (o.Item.Content is not null)
                    {
                        var b = (GitBlob)ht[o.Item.Name]!;

                        t.Add(o.Name, b!);
                    }
                    else
                    {
                        var to = (GitTree)ht[o.Item.Name]!;

                        t.Add(o.Name, to!);
                    }
                }

                var r = await t.WriteAndFetchAsync(repo);
                ht[m.Name] = r;
            }

            GitCommitWriter cw = GitCommitWriter.Create(Array.Empty<GitCommit>());
            cw.Tree = ((GitTree)ht[""]).AsWriter();

            cw.Author = cw.Committer = new GitSignature("BH", "bh@BH", new DateTimeOffset(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)));

            var cs = await cw.WriteAndFetchAsync(repo);


            var fsckOutput = await repo.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
            Assert.AreEqual($"dangling commit {cs.Id}", fsckOutput);

            Assert.AreEqual(items.Length + 1, repo.Objects.Count()); // F and C are the same empty tree
            Assert.IsFalse(items.Select(x => x.Name).Except(((IEnumerable<GitTreeItem>)repo.Commits[cs.Id]!.Tree.AllItems).Select(x => x.Path)).Any(), "All paths reached");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task CreateSvnTree2(bool noGit)
        {
            GitRepositoryInitArgs opts = new();

            if (noGit)
                opts.InitialConfiguration = new[] { ("ampscm.git-update-ref", "false") };

            using var repo = GitRepository.Init(TestContext.PerTestDirectory($"{noGit}"), opts);

            GitCommitWriter cw = GitCommitWriter.Create(new GitCommitWriter[0]);

            var items = new RepoItem[]
            {
                new RepoItem { Name = "iota", Content="This is dthe file 'iota'.\n" },
                new RepoItem { Name = "A" },
                new RepoItem { Name = "A/mu", Content="This is the file 'mu'.\n" },
                new RepoItem { Name = "A/B" },
                new RepoItem { Name = "A/B/lambda", Content="This is the file 'lambda'.\n"},
                new RepoItem { Name = "A/B/E", },
                new RepoItem { Name = "A/B/E/alpha", Content="This is the file 'alpha'.\n"},
                new RepoItem { Name = "A/B/E/beta", Content="This is the file 'beta'.\n" },
                new RepoItem { Name = "A/B/F" },
                new RepoItem { Name = "A/C" },
                new RepoItem { Name = "A/D" },
                new RepoItem { Name = "A/D/gamma", Content="This is the file 'gamma'.\n" },
                new RepoItem { Name = "A/D/G" },
                new RepoItem { Name = "A/D/G/pi", Content="This is the file 'pi'.\n" },
                new RepoItem { Name = "A/D/G/rho", Content="This is the file 'rho'.\n" },
                new RepoItem { Name = "A/D/G/tau", Content="This is the file 'tau'.\n" },
                new RepoItem { Name = "A/D/H" },
                new RepoItem { Name = "A/D/H/chi", Content = "This is the file 'chi'.\n" },
                new RepoItem { Name = "A/D/H/psi", Content = "This is the file 'psi'.\n" },
                new RepoItem { Name = "A/D/H/omega", Content = "This is the file 'omega'.\n" }
            };

            foreach (var item in items)
            {
                if (item.Content is not null)
                {
                    cw.Tree.Add(item.Name, GitBlobWriter.CreateFrom(Bucket.Create.FromUTF8(item.Content!)));
                }
                else
                {
                    cw.Tree.Add(item.Name, GitTreeWriter.CreateEmpty());
                }
            }

            cw.Author = cw.Committer = new GitSignature("BH", "bh@BH", new DateTimeOffset(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)));

            var cs = await cw.WriteAndFetchAsync(repo);

            var fsckOutput = await repo.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
            Assert.AreEqual($"dangling commit {cs.Id}", fsckOutput);

            Assert.AreEqual(items.Length + 1, repo.Objects.Count()); // F and C are the same empty tree
            Assert.IsFalse(items.Select(x => x.Name).Except(((IEnumerable<GitTreeItem>)repo.Commits[cs.Id]!.Tree.AllItems).Select(x => x.Path)).Any(), "All paths reached");

            cw = GitCommitWriter.Create(cs);

            cw.Author = cw.Committer = new GitSignature("BH", "bh@BH", new DateTimeOffset(new DateTime(2000, 1, 1, 1, 0, 0, DateTimeKind.Local)));

            cs = await cw.WriteAndFetchAsync(repo);

            fsckOutput = await repo.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
            Assert.AreEqual($"dangling commit {cs.Id}", fsckOutput);

            Assert.AreEqual(items.Length + 2, repo.Objects.Count()); // F and C are the same empty tree
            Assert.IsFalse(items.Select(x => x.Name).Except(((IEnumerable<GitTreeItem>)repo.Commits[cs.Id]!.Tree.AllItems).Select(x => x.Path)).Any(), "All paths reached");

            using (var t = repo.References.CreateUpdateTransaction())
            {
                t.Reason = "Testing";
                t.UpdateHead(cs.Id);
                await t.CommitAsync();
            }

            var ct = GitTagObjectWriter.Create(cs, "v0.1");
            ct.Message = "Tag second Commit";

            var tag = await ct.WriteAndFetchAsync(repo);

            using (var t = repo.References.CreateUpdateTransaction())
            {
                t.Reason = "Testing";
                t.Create($"refs/tags/{tag.Name}", tag.Id);
                await t.CommitAsync();
            }

            fsckOutput = await repo.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
            Assert.AreEqual($"", fsckOutput);
        }

        [TestMethod]
        public async Task TestFastImportFile()
        {
            var dir = TestContext.PerTestDirectory();
            using (var f = File.Create(Path.Combine(dir, "fast-export"), 16384, FileOptions.SequentialScan | FileOptions.DeleteOnClose))
            {
                using (var srcRepo = GitRepository.Open(GitTestEnvironment.GetRepository(GitTestDir.Packed)))
                using (var fastExportData = await srcRepo.GetPorcelain().FastExport(new() { All = true }))
                {
                    await fastExportData.WriteToAsync(f);
                }
                f.Position = 0;

                {
                    using var rp = GitRepository.Init(Path.Combine(dir, "rp"));

                    TestContext.WriteLine($"Count before: {rp.Objects.Count()}");
                    TestContext.WriteLine($"References: {rp.References.Count()}");
                    TestContext.WriteLine($"HEAD: {rp.Head}");

                    await rp.FastImportAsync(f.AsBucket());

                    TestContext.WriteLine($"Count after: {rp.Objects.Count()}");
                    TestContext.WriteLine($"References: {rp.References.Count()}");
                    TestContext.WriteLine($"HEAD: {rp.Head}");

                    await rp.GetPorcelain().CheckOut("HEAD");

                    var fsckOutput = await rp.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
                    Assert.AreEqual($"", fsckOutput);
                }
            }
        }

        [TestMethod]
        public async Task TestFastImportStream()
        {
            var dir = TestContext.PerTestDirectory();

            using var rp = GitRepository.Init(Path.Combine(dir, "rp"));

            TestContext.WriteLine($"Count before: {rp.Objects.Count()}");
            TestContext.WriteLine($"References: {rp.References.Count()}");
            TestContext.WriteLine($"HEAD: {rp.Head}");

            using (var srcRepo = GitRepository.Open(GitTestEnvironment.GetRepository(GitTestDir.Packed)))
            using (var pp = await srcRepo.GetPorcelain().FastExport(new() { All = true }))
            {
                await rp.FastImportAsync(pp);
            }

            TestContext.WriteLine($"Count after: {rp.Objects.Count()}");
            TestContext.WriteLine($"References: {rp.References.Count()}");
            TestContext.WriteLine($"HEAD: {rp.Head}");

            await rp.GetPorcelain().CheckOut("HEAD");

            var fsckOutput = await rp.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
            Assert.AreEqual($"", fsckOutput);
        }
    }
}
