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

            using var index = FileBucket.OpenRead(Path.Combine(repo.FullPath, ".git", "index"));


            using var dc = new GitCacheBucket(index, new() { IdType = GitIdType.Sha1 });

            _ = await dc.ReadAsync();
        }


        public static IEnumerable<object[]> GetIndexFormats => Enumerable.Range(GitCacheBucket.LowestSupportedFormat, GitCacheBucket.HighestSupportedFormat - GitCacheBucket.LowestSupportedFormat + 1).SelectMany(x => new object[][] { new object[] { x, false }, new object[] { x, true } });

        [TestMethod]
        [DynamicData(nameof(GetIndexFormats))]
        public async Task CheckReadFormatVersion(int version, bool addOptional)
        {
            var path = TestContext.PerTestDirectory((version, addOptional).ToString());
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


            using var dc = new GitCacheBucket(index, new() { IdType = GitIdType.Sha1 });

            await dc.ReadHeader();

            TestContext.WriteLine($"Version: {dc.IndexVersion}");

            while (await dc.ReadEntryAsync() is GitCacheEntry entry)
            {
                TestContext.WriteLine($"{entry.Name} - {entry}");
            }

            _ = await dc.ReadAsync();

            
        }
    }
}
