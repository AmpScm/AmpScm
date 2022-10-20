using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;
using AmpScm.Git.Client.Porcelain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests
{
    [TestClass]
    public class BundleTests
    {
        public TestContext TestContext { get; set; } = default!;

        [TestMethod]
        [DataRow(GitIdType.Sha1, 2)]
        [DataRow(GitIdType.Sha1, 3)]
        [DataRow(GitIdType.Sha256, 3)]
        public async Task ParseBundle(GitIdType idType, int version)
        {
            var pd = TestContext.PerTestDirectory($"{idType},{version}");
            using var repo = GitRepository.Open(
                idType switch
                {
                    GitIdType.Sha1 => GitTestEnvironment.GetRepository(GitTestDir.Greek),
                    GitIdType.Sha256 => GitTestEnvironment.GetRepository(GitTestDir.Sha256Greek),
                    _ => ""
                });

            var bundle = Path.Combine(pd, "bundle.bundle");

            await repo.GetPorcelain().Bundle(bundle, new() { Version = version });

            Assert.IsTrue(File.Exists(bundle));

            using var gbr = new GitBundleBucket(FileBucket.OpenRead(bundle));

            Assert.AreEqual(version, await gbr.ReadVersionAsync());

            string? key;
            do
            {
                (key, var v) = await gbr.ReadCapabilityAsync();

                if (!string.IsNullOrEmpty(key))
                    Console.WriteLine($"Capability {key}={v}");
            }
            while (key is { });

            GitId? id;
            do
            {
                (id, var v) = await gbr.ReadPrerequisiteAsync();

                if (id is not null)
                    Console.WriteLine($"Prereq {id}={v}");
            }
            while (id is not null);

            do
            {
                (id, var name) = await gbr.ReadReferenceAsync();

                if (id is not null)
                    Console.WriteLine($"Ref {id} {name}");
            }
            while (id is not null);

            var (bucket, idType2) = await gbr.ReadPackBucketAsync();

            Assert.AreEqual(idType, idType2);

            using var pb = new GitPackHeaderBucket(bucket);

            await pb.ReadUntilEofAsync();

            Assert.AreEqual("PACK", pb.GitType);
            Assert.AreEqual(2, pb.Version);
            Assert.IsTrue(pb.ObjectCount >= 27, $"ObjectCount={pb.ObjectCount}");
        }
    }
}
