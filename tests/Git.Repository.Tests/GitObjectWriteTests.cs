﻿using System.Diagnostics;
using AmpScm;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;
using AmpScm.Git.Client.Plumbing;
using AmpScm.Git.Client.Porcelain;
using AmpScm.Git.Objects;
using AmpScm.Git.Sets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests;

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

        GitCommitWriter cw = GitCommitWriter.Create(Array.Empty<GitCommit>());

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
            {
                await (await srcRepo.GetPorcelain().FastExport(new() { All = true }))
                    .WriteToAsync(f);
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
        {
            await rp.FastImportAsync(await srcRepo.GetPorcelain().FastExport(new() { All = true }));
        }

        TestContext.WriteLine($"Count after: {rp.Objects.Count()}");
        TestContext.WriteLine($"References: {rp.References.Count()}");
        TestContext.WriteLine($"HEAD: {rp.Head}");

        await rp.GetPorcelain().CheckOut("HEAD");

        var fsckOutput = await rp.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
        Assert.AreEqual($"", fsckOutput);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task CreateCommitChain(bool bare)
    {
        var dir = TestContext.PerTestDirectory($"q-{bare}");

        GitCommit A0, A1, A2, A3, B1;
        {
            using var rp = GitRepository.Init(dir, new GitRepositoryInitArgs { Bare = bare, InitialBranchName = "daisy" });

            GitCommitWriter gcw = GitCommitWriter.Create();
            gcw.Message = "Initial Commit";
            A0 = await gcw.WriteAndFetchAsync(rp);

            gcw = GitCommitWriter.Create(A0);
            gcw.Message = "Minor tweak";
            A1 = await gcw.WriteAndFetchAsync(rp);

            gcw = GitCommitWriter.Create(A1);
            gcw.Message = "Further tweak";
            A2 = await gcw.WriteAndFetchAsync(rp);

            gcw = GitCommitWriter.Create(A0);
            gcw.Message = "Other minor tweak";
            B1 = await gcw.WriteAndFetchAsync(rp);

            gcw = GitCommitWriter.Create(A2, B1);
            gcw.Message = "Merge tweaks together";
            A3 = await gcw.WriteAndFetchAsync(rp);

            using (var ru = rp.References.CreateUpdateTransaction())
            {
                ru.UpdateHead(A3.Id);

                await ru.CommitAsync();
            }

            Assert.AreEqual(0, Directory.GetFileSystemEntries(Path.Combine(rp.GitDirectory, "objects", "pack"), "*").Length, "Files on objects/pack");

            string commitGraphPath = Path.Combine(rp.GitDirectory, "objects", "info", "commit-graph");
            Assert.IsFalse(File.Exists(commitGraphPath), $"{commitGraphPath} does not exist");
            // This should get us an initial commit graph
            await rp.GetPorcelain().GC();

            Assert.IsTrue(File.Exists(commitGraphPath), $"{commitGraphPath} does exist");
            Assert.AreEqual(rp.IsBare ? 4 : 3, Directory.GetFileSystemEntries(Path.Combine(rp.GitDirectory, "objects", "pack"), "*").Length, "Files on objects/pack");

            // Doesn't use commit chain yet, as it wasn't detected before
            Assert.IsNotNull(rp.Head.Revisions.ToList());

            for (int i = 0; i < 256; i++)
            {
                var r = await rp.Objects.ResolveIdAsync(i.ToString("x2"));
            }

            // Refresh object sources. Here we find the commit graph (and the pack file)
            rp.ObjectRepository.Refresh();

            Assert.IsNotNull(rp.Head.Revisions.ToList());

            gcw = GitCommitWriter.Create(A3);
            gcw.Message = "Next update";
            var A4 = await gcw.WriteAndFetchAsync(rp);

            gcw = GitCommitWriter.Create(A4);
            gcw.Message = "Next update";
            var A5 = await gcw.WriteAndFetchAsync(rp);

            using (var ru = rp.References.CreateUpdateTransaction())
            {
                ru.UpdateHead(A5.Id);

                await ru.CommitAsync();
            }

            Assert.IsNotNull(rp.Head.Revisions.ToList());

            await rp.GetPlumbing().Repack();
            Assert.AreEqual(rp.IsBare ? 7 : 6, Directory.GetFileSystemEntries(Path.Combine(rp.GitDirectory, "objects", "pack"), "*").Length, "Files on objects/pack");

            await rp.GetPlumbing().CommitGraph(new() { Split = GitCommitGraphSplit.Split });

            Assert.IsFalse(File.Exists(commitGraphPath), $"{commitGraphPath} no longer exists");
            commitGraphPath += "s";
            Assert.IsTrue(Directory.Exists(commitGraphPath), $"{commitGraphPath} does exist");

#if NETFRAMEWORK
            if (Environment.OSVersion.Platform == PlatformID.Win32NT) // Fails on unix when file has to be re-opened
#endif
            {
                // We can still list the revisions old way...
                Assert.IsNotNull(rp.Head.Revisions.ToList());
            }

            // Refresh object sources. Here we find the commit graph chain, and other pack file
            rp.ObjectRepository.Refresh();

            Assert.AreEqual(3, Directory.GetFileSystemEntries(Path.Combine(rp.GitDirectory, "objects", "info", "commit-graphs"), "*").Length, "Files on objects/info/commit-graphs");

            // Walk the chained graphs
            Assert.IsNotNull(rp.Head.Revisions.ToList());

            for (int i = 0; i < 256; i++)
            {
                var r = await rp.Objects.ResolveIdAsync(i.ToString("x2"));
            }


            await rp.GetPlumbing().MultiPackIndex();

            // Refresh object sources. Here we find the multipack
            rp.ObjectRepository.Refresh();

            for (int i = 0; i < 256; i++)
            {
                var r = await rp.Objects.ResolveIdAsync(i.ToString("x2"));
            }

        }
    }


    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task DeltaOffsetTest(int seed)
    {
        var dir = TestContext.PerTestDirectory($"{seed}");
        using var repo = GitRepository.Init(dir);

        List<string> lines = new List<string>();

        var cw = GitCommitWriter.CreateFromTree(new GitTreeWriter()
        {
            ["iota"] = GitBlobWriter.CreateFrom(Bucket.Empty)
        });
        var c = await cw.WriteAndFetchAsync(repo);

        Random r = new Random(seed);

        for (int i = 1; i < 40; i++)
        {
            for (int j = 0; j < 5; j++)
                switch (r.Next(20))
                {
                    case 0 when (lines.Count > 0):
                        lines.RemoveAt(r.Next(lines.Count));
                        break;

                    case 1 when (lines.Count > 5):
                        var from = r.Next(lines.Count);
                        var s = lines[from];
                        lines.RemoveAt(from);
                        lines.Insert(r.Next(lines.Count + 1), s);
                        break;

                    default:
                        lines.Insert(r.Next(lines.Count + 1), Guid.NewGuid().ToString());
                        break;
                }

            cw = GitCommitWriter.CreateFromTree(new GitTreeWriter()
            {
                ["iota"] = GitBlobWriter.CreateFrom(Bucket.Create.FromUTF8(string.Join("\n", lines)))
            });
            cw.Parents = new[] { c };

            c = await cw.WriteAndFetchAsync(repo);
        }

        using (var sh = repo.References.CreateUpdateTransaction())
        {
            sh.UpdateHead(c.Id);
            await sh.CommitAsync();
        }

        await repo.GetPorcelain().GC(new() { Aggressive = true });

        repo.ObjectRepository.Refresh(); // Find packs


        List<(int depth, GitId id)> depths = new();

        foreach (var blob in repo.Blobs)
        {
            await using var b = blob.AsBucket();

            if (b is GitPackObjectBucket gob)
            {
                depths.Add((await gob.ReadDeltaCountAsync(), blob.Id));
            }
        }

        Assert.IsTrue(depths.Max(x => x.depth) > 18, "Have depth > 18");

        foreach (var (_, id) in depths.OrderByDescending(x => x.depth).Take(5))
        {
            int sz;
            {
                var b = repo.Blobs[id]!.AsBucket();
                sz = (int)await b.ReadRemainingBytesAsync();

                await using var q = (GitObjectType.Blob.CreateHeader(sz) + b).GitHash(GitIdType.Sha1, x => { if (x != id) throw new InvalidOperationException("Id mismatch"); });

                await q.ReadSkipAsync(long.MaxValue);
            }

            for (int i = 0; i < 10; i++)
            {
                await using var b = repo.Blobs[id]!.AsBucket();

                await b.SeekAsync(r.Next(sz));

                await b.ReadAtLeastAsync(Bucket.MaxRead, throwOnEndOfStream: false);
            }
        }
    }

    [TestMethod]
    public async Task VerifyMergeTag()
    {
        var dir = await TestContext.CreateCloneAsync();
        using var repo = GitRepository.Open(dir);

        var iota = Path.Combine(dir, "iota");
        File.WriteAllText(iota, "This is the updated IOTA");

        GitId id = repo.Head.Id!;

        await repo.GetPorcelain().Commit(new[] { iota }, new() { Message = "Updated IOTA" });

        int n = repo.Objects.Count();
        Assert.AreNotEqual(id, repo.Head.Id);

        await repo.GetPorcelain().Tag("v0.2", new() { Message = "Tag IOTA" });

        Assert.AreEqual(n + 1, repo.Objects.Count());

        Assert.AreEqual(2, repo.TagObjects.Count());

        using (var t = repo.References.CreateUpdateTransaction())
        {
            t.Create("refs/base", id);
            await t.CommitAsync();
        }

        await repo.GetPorcelain().CheckOut("base");

        var mu = Path.Combine(dir, "A/mu");
        File.WriteAllText(mu, "This is the updated MU");

        await repo.GetPorcelain().Commit(new[] { mu }, new() { Message = "Updated MU" });
        await repo.GetPorcelain().Merge("refs/tags/v0.2", new() { Message = "Merge v0.2", FastForward = AllowAlwaysNever.Never });

        var fsckOutput = await repo.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
        Assert.AreEqual($"", fsckOutput);

        GitId newHead = repo.References.Head.Id!;

        Assert.AreNotEqual(id, newHead);

        var cc = await repo.Commits.GetAsync(newHead);
        Assert.IsNotNull(cc);

        Assert.AreEqual("Merge v0.2\n", cc.Message);
        Assert.AreEqual(2, cc!.ParentCount);
        Trace.WriteLine($"{cc.Parents[0].Id:x12} - {cc.Parents[0].Summary}");
        Trace.WriteLine($"{cc.Parents[1].Id:x12} - {cc.Parents[1].Summary}");

        Assert.IsNull(cc.MergeTags[1], "No mergetag created by git");


        var tag = repo.TagObjects.FirstOrDefault(x => x.Name == "v0.2");

        Assert.IsNotNull(tag, "Found tag");

        GitCommitWriter gcw = cc.AsWriter();

        gcw.MergeTags = new[] { tag };
        gcw.Message = "Rewritten";

        var rewritten = await gcw.WriteToAsync(repo);

        using (var t = repo.References.CreateUpdateTransaction())
        {
            t.Create("refs/heads/rewritten", rewritten);
            await t.CommitAsync();
        }


        Trace.WriteLine($"Rewritten: {rewritten}");


        var (_, outTxt) = await repo.GetPlumbing().RunRawCommand("cat-file", "-p", rewritten.ToString());
        Trace.WriteLine("--");
        Trace.WriteLine(outTxt);
        Trace.WriteLine("--");

        fsckOutput = await repo.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
        Assert.AreEqual($"", fsckOutput);

        GitCommit? rc = await repo.Commits.GetAsync(rewritten);
        Assert.IsNotNull(rc);
        Assert.AreEqual("Rewritten", rc.Message);

        Assert.AreEqual(2, rc.ParentCount);
        Assert.IsNull(rc.MergeTags[0]);
        Assert.IsNotNull(rc.MergeTags[1]);
        Assert.AreEqual(tag.Id, rc.MergeTags[1]!.Id);
    }
}
