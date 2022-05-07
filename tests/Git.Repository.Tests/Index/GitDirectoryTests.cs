using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Git;
using AmpScm.Git.Client.Plumbing;
using AmpScm.Git.Client.Porcelain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests.Index
{
    [TestClass]
    public class GitDirectoryTests
    {
        public TestContext TestContext { get; set; } = default!;

        [TestMethod]
        public async Task CheckIndexAsync()
        {
            using var repo = GitRepository.Open(GitTestEnvironment.GetRepository(GitTestDir.Packed));

            using var index = FileBucket.OpenRead(Path.Combine(repo.WorktreePath, "index"));


            using var dc = new GitDirectoryBucket(index, new() { IdType = GitIdType.Sha1 });

            await dc.ReadHeaderAsync();

            TestContext.WriteLine($"Version: {dc.IndexVersion}");

            while (await dc.ReadEntryAsync() is GitDirectoryEntry entry)
            {
                TestContext.WriteLine($"{entry.Name} - {entry}");
            }
        }


        public static IEnumerable<object[]> GetIndexFormats => Enumerable.Range(GitDirectoryBucket.LowestSupportedFormat, GitDirectoryBucket.HighestSupportedFormat - GitDirectoryBucket.LowestSupportedFormat + 1).SelectMany(x => Enumerable.Range(0, 4).Select(y => new object[] { x, ((int)y & 1) != 0, ((int)y & 2) != 0 }));

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

            using var index = FileBucket.OpenRead(Path.Combine(repo.FullPath, ".git", "index"));


            using var dc = new GitDirectoryBucket(index, new() { IdType = GitIdType.Sha1, LookForEndOfIndex = optimize });

            await dc.ReadHeaderAsync();

            TestContext.WriteLine($"Version: {dc.IndexVersion}");

            while (await dc.ReadEntryAsync() is GitDirectoryEntry entry)
            {
                TestContext.WriteLine($"{entry.Name} - {entry}");
            }
        }

        [TestMethod]
        public async Task CheckReadSparse()
        {
            // This scenario is not really exposed by plain git yet, but this close
            // creates a new sparse checkout with sparse index for cone layout, where trees are
            // stored as tree objects directly in the index, instead of cached as trees.
            var path = TestContext.PerTestDirectory();
            {
                using var gc = GitRepository.Open(GitTestEnvironment.GetRepository(GitTestDir.Packed));
                await gc.GetPorcelain().Clone(gc.FullPath, path, new()
                {
                    Sparse = true,
                    InitialConfiguration = new[]
                    {
                        ("core.sparseCheckout", "true"),
                        ("core.sparseCheckoutCone", "true"),
                        ("index.sparse", "true")
                    }
                });
            }

            using var repo = GitRepository.Open(path);
            Console.WriteLine(path);

            using var dc = new GitDirectoryBucket(repo.WorktreePath);

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
                    Shared = true,
                    InitialConfiguration = new[]
                    {
                        ("core.splitIndex", "true"),
                        ("index.recordEndOfIndexEntries", $"{optimize}"),
                        ("index.version", "4")
                    }
                });
            }

            using var repo = GitRepository.Open(path);
            List<GitDirectoryEntry> entries = new List<GitDirectoryEntry>();
            using (var dc2 = new GitDirectoryBucket(repo.WorktreePath, new GitDirectoryOptions { LookForEndOfIndex = lookFor }))
            {
                while (await dc2.ReadEntryAsync() is GitDirectoryEntry q)
                {
                    entries.Add(q);
                }
            }

            await repo.GetPlumbing().UpdateIndex(new() { SplitIndex = true });

            File.WriteAllText(Path.Combine(path, "miota"), "QQQ");
            File.WriteAllText(Path.Combine(path, "A", "mu"), "QQQ");

            File.AppendAllText(Path.Combine(path, "README.md"), " ");

            await repo.GetPlumbing().RunRawCommand("add", "miota", "A/mu", "README.md");



            using var dc = new GitDirectoryBucket(repo.WorktreePath, new GitDirectoryOptions
            {
                LookForEndOfIndex = lookFor
            });

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

            _ = await dc.ReadAsync();
        }

        [TestMethod]
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

            using var dc = new GitDirectoryBucket(repo.WorktreePath);

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
            Console.WriteLine(path);

            try
            {
                using var idx = FileBucket.OpenRead(Path.Combine(repo.WorktreePath, "index"));

                using var dc = new GitDirectoryBucket(idx);

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
}
