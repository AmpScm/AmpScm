using System.Diagnostics;
using AmpScm;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Git;
using AmpScm.Git.Client.Plumbing;
using AmpScm.Git.Client.Porcelain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests.Index;

[TestClass]
public class GitDirectoryTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task CheckIndexAsync()
    {
        using var repo = GitRepository.Open(GitTestEnvironment.GetRepository(GitTestDir.Packed));

        var index = FileBucket.OpenRead(Path.Combine(repo.WorkTreeDirectory, "index"));
        await using var dc = new GitDirectoryBucket(index, new() { IdType = GitIdType.Sha1 });

        await dc.ReadHeaderAsync();

        TestContext.WriteLine($"Version: {dc.IndexVersion}");

        while (await dc.ReadEntryAsync() is GitDirectoryEntry entry)
        {
            TestContext.WriteLine($"{entry.Name} - {entry}");
        }
    }


    public static IEnumerable<object[]> GetIndexFormats => Enumerable.Range(GitDirectoryBucket.LowestSupportedFormat, GitDirectoryBucket.HighestSupportedFormat - GitDirectoryBucket.LowestSupportedFormat + 1).SelectMany(x => Enumerable.Range(0, 4).Select(y => new object[] { x, (y & 1) != 0, (y & 2) != 0 }));

    [TestMethod]
    [DynamicData(nameof(GetIndexFormats))]
    public async Task CheckReadFormatVersion(int version, bool addOptional, bool optimize)
    {
        var path = TestContext.PerTestDirectory((version, addOptional, optimize).ToString());
        {
            using var gc = GitRepository.Open(GitTestEnvironment.GetRepository(GitTestDir.Packed));

            await gc.GetPorcelain().Clone(gc.FullPath, path, new()
            {
                Shared = true,
                InitialConfiguration = new[]
                {
                    ("index.version", $"{version}"),
                }
                .Concat(addOptional ? new[]
                {
                    ("index.recordEndOfIndexEntries", "true"),
                    ("index.recordOffsetTable", "true")
                } : Enumerable.Empty<(string, string)>())
            });
        }

        using var repo = GitRepository.Open(path);

        var index = FileBucket.OpenRead(Path.Combine(repo.FullPath, ".git", "index"));
        await using var dc = new GitDirectoryBucket(index, new() { IdType = GitIdType.Sha1, LookForEndOfIndex = optimize });

        await dc.ReadHeaderAsync();

        TestContext.WriteLine($"Version: {dc.IndexVersion}");

        while (await dc.ReadEntryAsync() is GitDirectoryEntry entry)
        {
            TestContext.WriteLine($"{entry.Name} - {entry}");
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task CheckReadSparse(bool cone)
    {
        // This scenario is not really exposed by plain git yet, but this close
        // creates a new sparse checkout with sparse index for cone layout, where trees are
        // stored as tree objects directly in the index, instead of cached as trees.
        var path = TestContext.PerTestDirectory($"{cone}");
        {
            using var gc = GitRepository.Open(GitTestEnvironment.GetRepository(GitTestDir.Packed));
            await gc.GetPorcelain().Clone(gc.FullPath, path, new()
            {
                Sparse = true,
                InitialConfiguration = new[]
                {
                    ("core.sparseCheckout", "true"),
                    ("core.sparseCheckoutCone", $"{cone}"),
                    ("index.sparse", "true")
                }
            });
        }

        using var repo = GitRepository.Open(path);
        Trace.WriteLine(path);

        await using var dc = new GitDirectoryBucket(repo.WorkTreeDirectory);

        await dc.ReadHeaderAsync();

        TestContext.WriteLine($"Version: {dc.IndexVersion}");

        while (await dc.ReadEntryAsync() is GitDirectoryEntry entry)
        {
            //TestContext.WriteLine($"{entry.Name} - {entry}");
        }
    }

    [TestMethod]
    [DataRow(false, true)]
    [DataRow(false, false)]
    [DataRow(true, true)]
    [DataRow(true, false)]
    public async Task CheckReadSplit(bool optimize, bool lookFor)
    {
        var path = TestContext.PerTestDirectory($"{optimize}{lookFor}");
        {
            using var gc = GitRepository.Open(GitTestEnvironment.GetRepository(GitTestDir.Packed));
            await gc.GetPorcelain().Clone(gc.FullPath, path, new()
            {
                InitialConfiguration = new[]
                {
                    ("core.splitIndex", "true"),
                    ("index.recordEndOfIndexEntries", $"{optimize}"),
                    ("index.version", "4")
                }
            });
        }

        await Task.Delay(1000);

        using var repo = GitRepository.Open(path);
        TestContext.WriteLine(repo.WorkTreeDirectory);

        Assert.AreEqual(Path.Combine(repo.FullPath, ".git"), repo.GitDirectory);
        Assert.AreEqual(Path.Combine(repo.FullPath, ".git"), repo.WorkTreeDirectory);

        Assert.AreEqual(path, repo.FullPath);
        List<GitDirectoryEntry> entries = new List<GitDirectoryEntry>();
        await using (var dc2 = new GitDirectoryBucket(repo.WorkTreeDirectory, new GitDirectoryOptions { LookForEndOfIndex = lookFor }))
        {
            while (await dc2.ReadEntryAsync() is GitDirectoryEntry q)
            {
                entries.Add(q);
            }

            GC.KeepAlive(dc2);
        }

        foreach (var p in new DirectoryInfo(repo.WorkTreeDirectory).EnumerateFileSystemInfos())
        {
            TestContext.WriteLine($"{p.Name} - {p.Attributes}");
        }

        TestContext.WriteLine("");

        foreach (Process p in Process.GetProcesses())
        {
            try
            {
                string name = p.ProcessName;
                if (name.Contains("git") || name.Contains('-'))
                    Trace.WriteLine(p.ProcessName);
            }
            catch { }
        }

        Assert.IsTrue(File.Exists(Path.Combine(repo.WorkTreeDirectory, "index")), "Has index");
        Assert.IsFalse(Directory.EnumerateFiles(repo.WorkTreeDirectory, "sharedindex.*").Any(), "No shared index yet");
        Assert.IsFalse(File.Exists(Path.Combine(repo.WorkTreeDirectory, "index.lock")), "Has no index lockfile");

        string suffix = new string('\n', 1024);

        File.WriteAllText(Path.Combine(path, "miota"), "QQQ" + suffix);
        File.WriteAllText(Path.Combine(path, "A", "mu"), "QQQ" + suffix);
        File.AppendAllText(Path.Combine(path, "README.md"), " " + suffix);

        await Task.Delay(1000);
        await repo.GetPorcelain().Add(new[] { "miota", "A/mu", "README.md" });

        Assert.IsTrue(File.Exists(Path.Combine(repo.WorkTreeDirectory, "index")), "After add has index");
        Assert.IsTrue(Directory.EnumerateFiles(repo.WorkTreeDirectory, "sharedindex.*").Any(), "After add has shared index");

        await using (var dc = new GitDirectoryBucket(repo.WorkTreeDirectory, new GitDirectoryOptions { LookForEndOfIndex = lookFor }))
        {
            await dc.ReadHeaderAsync();

            TestContext.WriteLine($"Version: {dc.IndexVersion}");

            int n = 0;
            string? ln = null;
            while (await dc.ReadEntryAsync() is GitDirectoryEntry entry)
            {
                TestContext.WriteLine($"{entry.Name} - {entry}");
                if (ln is not null)
                {
                    Assert.IsTrue(string.CompareOrdinal(entry.Name, ln) >= 0);
                }
                ln = entry.Name;
                n++;
            }

            Assert.AreEqual(entries.Count + 2, n);
        }

        await foreach (var v in await repo.GetPorcelain().Status(new() { Untracked = GitStatusUntrackedMode.Normal }))
        {
            TestContext.WriteLine(v);
        }
    }

    [TestMethod]
    [Ignore]
    public async Task CheckWithFsMonitor()
    {
        var path = TestContext.PerTestDirectory();
        {
            using var gc = GitRepository.Open(GitTestEnvironment.GetRepository(GitTestDir.Packed));
            await gc.GetPorcelain().Clone(gc.FullPath, path, new()
            {
                Shared = true,
                InitialConfiguration = new[]
                {
                    ("core.fsmonitor", "true"),
                }
            });
        }

        using var repo = GitRepository.Open(path);
        await repo.GetPlumbing().UpdateIndex();

        await using var dc = new GitDirectoryBucket(repo.WorkTreeDirectory);

        await dc.ReadHeaderAsync();

        TestContext.WriteLine($"Version: {dc.IndexVersion}");

        while (await dc.ReadEntryAsync() is GitDirectoryEntry entry)
        {
            //TestContext.WriteLine($"{entry.Name} - {entry}");
        }

        _ = await dc.ReadAsync();
    }



    [TestMethod]
    public async Task CheckNoReadSplit()
    {
        var path = TestContext.PerTestDirectory();
        {
            using var gc = GitRepository.Open(GitTestEnvironment.GetRepository(GitTestDir.Packed));
            await gc.GetPorcelain().Clone(gc.FullPath, path, new()
            {
                Shared = true,
                InitialConfiguration = new[]
                {
                    ("core.splitIndex", "true"),
                    ("index.version", "4"),
                }
            });

            //TestContext.WriteLine(File.ReadAllText(Path.Combine(path, ".git", "config")));
        }

        using var repo = GitRepository.Open(path);

        await repo.GetPlumbing().UpdateIndex(new() { SplitIndex = true });
        Trace.WriteLine(path);

        try
        {
            var idx = FileBucket.OpenRead(Path.Combine(repo.WorkTreeDirectory, "index"));
            await using var dc = new GitDirectoryBucket(idx);

            while (await dc.ReadEntryAsync() is GitDirectoryEntry entry)
            {
                //TestContext.WriteLine($"{entry.Name} - {entry}");
            }

            Assert.Fail("Expected exception on not being able to read split index without passing path");
        }
        catch (GitBucketException x) when (x.Message.Contains("split index"))
        {
            return;
        }
    }
}
