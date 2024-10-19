using System.Diagnostics;
using System.IO.Compression;
using AmpScm;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;
using AmpScm.Git.Client.Porcelain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests;

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

        await using var gbr = new GitBundleBucket(FileBucket.OpenRead(bundle));

        Assert.AreEqual(version, await gbr.ReadVersionAsync());

        string? key;
        do
        {
            (key, var v) = await gbr.ReadCapabilityAsync();

            if (!string.IsNullOrEmpty(key))
                Trace.WriteLine($"Capability {key}={v}");
        }
        while (key is { });

        GitId? id;
        do
        {
            (id, var v) = await gbr.ReadPrerequisiteAsync();

            if (id is not null)
                Trace.WriteLine($"Prereq {id}={v}");
        }
        while (id is not null);

        do
        {
            (id, var name) = await gbr.ReadReferenceAsync();

            if (id is not null)
                Trace.WriteLine($"Ref {id} {name}");
        }
        while (id is not null);

        var (bucket, idType2) = await gbr.ReadPackBucketAsync();

        Assert.AreEqual(idType, idType2);

        await using var pb = new GitPackHeaderBucket(bucket);

        await pb.ReadUntilEofAsync();

        Assert.AreEqual("PACK", pb.GitType);
        Assert.AreEqual(2, pb.Version);
        Assert.IsTrue(pb.ObjectCount >= 27, $"ObjectCount={pb.ObjectCount}");
    }


    [TestMethod]
    public async Task WriteArchive()
    {
        var pd = TestContext.PerTestDirectory();
        using var repo = GitRepository.Open(GitTestEnvironment.GetRepository(GitTestDir.Greek));

        var archive = Path.Combine(pd, "my.zip");

        await repo.GetPorcelain().Archive(archive, "HEAD", new() { Format = GitArchiveFormat.Zip });

        Assert.IsTrue(File.Exists(archive));
#if !NETFRAMEWORK
        var zf = ZipFile.OpenRead(archive);

        Assert.AreEqual(20, zf.Entries.Count);
#endif
    }
}
