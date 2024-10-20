﻿using AmpScm;
using AmpScm.Buckets;
using AmpScm.Git;
using AmpScm.Git.Client.Plumbing;
using AmpScm.Git.Client.Porcelain;
using AmpScm.Git.Objects;
using AmpScm.Git.References;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests;

[TestClass]
public class GitRepositoryWalks
{
    public TestContext TestContext { get; set; } = null!;

    private static List<string> TestRepositories { get; } = GetTestRepositories().Concat(GitTestEnvironment.TestDirectories.Select(x => $">{x}")).ToList();

    private static IEnumerable<string> GetTestRepositories()
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
        if (path.Contains('>'))
            path = GitTestEnvironment.GetRepository((GitTestDir)Enum.Parse(typeof(GitTestDir), path.Substring(1)));

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
        int n = 0; ;
        await foreach (var r in repo.References)
        {
            n++;
            TestContext.WriteLine($"{r.Name} {r.ShortName.PadRight(15)} - {r.Commit?.Id:x7} - {r.Commit?.Author}");
        }

        if (!repo.IsShallow && repo.Head.Commit.ParentCount > 0)
            Assert.IsNotNull(repo.Commits.FirstOrDefault(x => x.Parents.Count > 1), "Repository has merges");

        Assert.IsTrue(repo.References.Any(), "Repository has references");
        Assert.IsNotNull(repo.References.Any(x => x.Name == "HEAD"), "Has reference called HEAD-1");
        Assert.IsNotNull(repo.References["HEAD"], "Has reference called HEAD-2");
    }

    [TestMethod]
    public async Task CompareInitAmpVsGit()
    {
        string p = TestContext.PerTestDirectory("1");
        string p2 = TestContext.PerTestDirectory("2");
        {
            using var repo = GitRepository.Open(GitTestEnvironment.GetRepository(GitTestDir.Bare));

            await repo.GetPorcelain().Init(p);

            using var rep2 = GitRepository.Init(p2);
        }

        var r = GitRepository.Open(p);

        Assert.IsFalse(r.Commits.Any(), "No commits");
        Assert.IsFalse(r.Blobs.Any(), "No blobs");
        Assert.IsFalse(r.Remotes.Any(), "No remotes");

        TestContext.WriteLine(File.ReadAllText(Path.Combine(r.WorkTreeDirectory, "config")));

        foreach (var f in Directory.GetFileSystemEntries(r.WorkTreeDirectory, "*", SearchOption.AllDirectories))
        {
            string subPath = f.Substring(r.WorkTreeDirectory.Length + 1);
            TestContext.WriteLine(subPath);

            if (Directory.Exists(f))
            {
                if (subPath == "branches")
                    continue; // Why is this created on *nix and not on Windows??
                var f2 = Path.Combine(p2, ".git", subPath);
                Assert.IsTrue(Directory.Exists(f2), $"Directory '{f2}' exists");
            }
            else if (File.Exists(f) && !subPath.StartsWith("hooks"))
            {
                var f2 = Path.Combine(p2, ".git", subPath);
                Assert.IsTrue(File.Exists(f2), $"File '{f2}' exists");
            }
        }
    }

    public static IEnumerable<object[]> GitIdTypes => Enum.GetValues(typeof(GitIdType)).Cast<GitIdType>().Where(x => x != GitIdType.None).Select(x => new object[] { x });

    [TestMethod]
    [DynamicData(nameof(GitIdTypes))]
    public async Task CanReadType(GitIdType idType)
    {
        string p = TestContext.PerTestDirectory(idType.ToString());
        {
            using var repo = GitRepository.Open(GitTestEnvironment.GetRepository(GitTestDir.Bare));

            await repo.GetPorcelain().Init(p, new GitInitArgs { IdType = idType });
        }

        var r = GitRepository.Open(p);

        Assert.IsFalse(r.Commits.Any(), "No commits");
        Assert.IsFalse(r.Blobs.Any(), "No blobs");
        Assert.IsFalse(r.Remotes.Any(), "No remotes");

        var cw = GitCommitWriter.Create();
        cw.Author = cw.Committer = new GitSignature("A A", "A@A", new DateTime(2020, 2, 2, 0, 0, 0, DateTimeKind.Utc));

        var c = await cw.WriteAndFetchAsync(r);

        Assert.AreEqual(idType, c.Id.Type);

        using (var t = r.References.CreateUpdateTransaction())
        {
            t.UpdateHead(c.Id);
            await t.CommitAsync();
        }

        Assert.AreEqual("", await r.GetPlumbing().ConsistencyCheck(new() { Full = true }));
        Assert.AreEqual(idType, r.Head.Id!.Type);
        Assert.AreEqual(c.Id, r.Head.Id);
    }

    [TestMethod]
    [DynamicData(nameof(GitIdTypes))]
    public async Task CanCreateType(GitIdType idType)
    {
        string p = TestContext.PerTestDirectory(idType.ToString());
        var r = GitRepository.Init(p, new GitRepositoryInitArgs { IdType = idType });

        Assert.IsFalse(r.Commits.Any(), "No commits");
        Assert.IsFalse(r.Blobs.Any(), "No blobs");
        Assert.IsFalse(r.Remotes.Any(), "No remotes");

        var cw = GitCommitWriter.Create();
        cw.Author = cw.Committer = new GitSignature("A A", "A@A", new DateTime(2020, 2, 2, 0, 0, 0, DateTimeKind.Utc));

        var c = await cw.WriteAndFetchAsync(r);

        Assert.AreEqual(idType, c.Id.Type);

        using (var t = r.References.CreateUpdateTransaction())
        {
            t.UpdateHead(c.Id);
            await t.CommitAsync();
        }

        Assert.AreEqual("", await r.GetPlumbing().ConsistencyCheck(new() { Full = true }));
        Assert.AreEqual(idType, r.Head.Id!.Type);
        Assert.AreEqual(c.Id, r.Head.Id);
    }

    [TestMethod]
    [DynamicData(nameof(TestRepositoryArgs))]
    public async Task WalkHistory(string path)
    {
        //bool small = false;
        if (path.Contains('>'))
        {
            path = GitTestEnvironment.GetRepository((GitTestDir)Enum.Parse(typeof(GitTestDir), path.Substring(1)));
            return;
        }

        using var repo = GitRepository.Open(path);

        if (repo.IsShallow || repo.Head.Commit!.ParentCount == 0)
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


    public static IEnumerable<object[]> TestRepositoryArgsBitmapAndRev => TestRepositoryArgs.Where(x => x[0] is string s && (s.Contains('>') || Directory.EnumerateFiles(Path.Combine(s, ".git", "objects", "pack"), "*.bitmap").Any()));
    [TestMethod]
    [DynamicData(nameof(TestRepositoryArgsBitmapAndRev))]
    public async Task WalkObjectsViaBitmap(string path)
    {
        if (path.Contains('>'))
            path = GitTestEnvironment.GetRepository((GitTestDir)Enum.Parse(typeof(GitTestDir), path.Substring(1)));

        using var repo = await GitRepository.OpenAsync(path);

        Assert.IsTrue(repo.Commits.Count() > 0);
        Assert.IsTrue(repo.Trees.Count() > 0);
        Assert.IsTrue(repo.Blobs.Count() > 0);
        Assert.IsTrue(repo.TagObjects.Count() > 0);
    }

    [TestMethod]
    [DynamicData(nameof(TestRepositoryArgs))]
    public async Task WalkTreeLinkItems(string path)
    {
        if (path.Contains('>'))
            path = GitTestEnvironment.GetRepository((GitTestDir)Enum.Parse(typeof(GitTestDir), path.Substring(1)));

        using var repo = await GitRepository.OpenAsync(path);

        GitTree tree = repo.Head.Tree!;

        Assert.IsNotNull(tree, "HEAD has tree");

        bool hasModules = tree.AllFiles.TryGet(".gitmodules", out _);
        bool foundModule = false;
        foreach (var item in tree)
        {
            switch (item.ElementType)
            {
                case GitTreeElementType.GitCommitLink:
                    foundModule = true;
                    break;
                case GitTreeElementType.SymbolicLink:

                    await using (var b = ((GitBlob)item.GitObject).AsBucket())
                    {
                        var bb = await b.ReadAtLeastAsync(1024, throwOnEndOfStream: false);

                        var target = bb.ToUTF8String();

                        Assert.IsTrue(tree.AllItems.TryGet(target, out _), $"Can find {target}");
                    }
                    break;
            }
        }

        Assert.AreEqual(hasModules, foundModule);
    }

    [TestMethod]
    public async Task WalkWorkTreeWorkingCopy()
    {
        var path = TestContext.PerTestDirectory();
        var path2 = TestContext.PerTestDirectory("2");
        {
            using GitRepository gc = GitRepository.Open(typeof(GitRepositoryWalks).Assembly.Location);
            await gc.GetPorcelain().Clone(gc.FullPath, path2, new() { Shared = true, NoCheckOut = true });
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

    [TestMethod]
    public async Task StashSome()
    {
        var path = TestContext.PerTestDirectory();
        {
            using GitRepository gc = GitRepository.Open(GitTestEnvironment.GetRepository());
            await gc.GetPorcelain().Clone(gc.FullPath, path, new() { Shared = true });
        }

        string newFile;
        File.WriteAllText(newFile = Path.Combine(path, "newFile"), "Some text!");

        using var repo = GitRepository.Open(path);

        await repo.GetPorcelain().Add(newFile);

        Assert.IsFalse(repo.References.Any(x => x.Name.Contains("stash")), "No stash reference found");

        Assert.AreEqual(0, repo.Stashes.Count());

        await repo.GetPorcelain().Stash(new GitStashArgs { Command = GitStashCommand.Push });

        Assert.IsFalse(repo.References.Any(x => x.Name.Contains("stash")), "No stash reference found");

        Assert.AreEqual(1, repo.Stashes.Count());
        Assert.IsNotNull(repo.Stashes[0].Message);
    }
}
