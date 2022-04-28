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
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);

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
                using var gc = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);
                List<string> args = new() { gc.FullPath, path, "-s", "-c", $"index.version={version}" };

                if (addOptional)
                {
                    args.AddRange(new[] { "-c", "index.recordEndOfIndexEntries=true" });
                    args.AddRange(new[] { "-c", "index.recordOffsetTable=true" });
                }
                await gc.GetPlumbing().RunRawCommand("clone", args.ToArray());
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
                using var gc = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);
                await gc.GetPlumbing().RunRawCommand("clone",
                    gc.FullPath,
                    path, "-s",
                    "-c", "core.sparseCheckout=true",
                    "-c", "core.sparseCheckoutCone=true",
                    "-c", "index.sparse=true",
                    "--sparse"
                    );
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
        [DataRow(true)]
        [DataRow(false)]
        public async Task CheckReadSplit(bool optimize)
        {
            var path = TestContext.PerTestDirectory($"{optimize}");
            {
                using var gc = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);
                await gc.GetPlumbing().RunRawCommand("clone",
                    gc.FullPath,
                    path, "-s",
                    "-c", $"core.splitIndex=true",
                    //"-c", $"splitIndex.maxPercentChange=0",
                    "-c", $"index.recordEndOfIndexEntries={optimize}",
                    "-c", $"index.version=4"
                    );
            }

            using var repo = GitRepository.Open(path);

            await repo.GetPlumbing().RunRawCommand("update-index", "--split-index");
            Console.WriteLine(path);

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
        public async Task CheckWithFsMonitor()
        {
            var path = TestContext.PerTestDirectory();
            {
                using var gc = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);
                await gc.GetPlumbing().RunRawCommand("clone",
                    gc.FullPath,
                    path, "-s",
                    "-c", $"core.fsmonitor=true"
                    );
            }

            using var repo = GitRepository.Open(path);

            await repo.GetPlumbing().RunRawCommand("update-index");
            Console.WriteLine(path);

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
                using var gc = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);
                await gc.GetPlumbing().RunRawCommand("clone",
                    gc.FullPath,
                    path, "-s",
                    "-c", $"core.splitIndex=true",
                    "-c", $"index.version=4"
                    );

                //TestContext.WriteLine(File.ReadAllText(Path.Combine(path, ".git", "config")));
            }

            using var repo = GitRepository.Open(path);

            await repo.GetPlumbing().RunRawCommand("update-index", "--split-index");
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
