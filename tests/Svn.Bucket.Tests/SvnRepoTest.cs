using AmpScm;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;
using AmpScm.Buckets.Subversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Svn.Bucket.Tests;

[TestClass]
public class SvnRepoTest
{
    private static readonly object _setup = new();
    public TestContext TestContext { get; set; } = default!;

    public static Uri RepositoryUrl { get; private set; } = default!;
    public static string RepositoryPath { get; private set; } = default!;

    [TestInitialize]
    public void EnsureSetup()
    {
        lock (_setup)
        {
            if (RepositoryUrl is not null)
                return;

            string root = TestContext.TestRunDirectory!;
            var dir = Path.Combine(root, "repo");
            var tmp = Path.Combine(root, "tmp");
            Directory.CreateDirectory(dir);

            string df;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                df = "file:///" + dir + "/";
            else
                df = "file://" + dir + "/";

            if (!Uri.TryCreate(df, UriKind.Absolute, out var reposUri))
                Assert.Fail("Couldn't create proper uri");

            RunSvnAdmin("create", dir, "--pre-1.6-compatible");
            RunSvn("mkdir", "--parents", new Uri(reposUri, "trunk/A").AbsoluteUri, "-m", "Create trunk/A", "-q");

            string iota = Path.Combine(tmp, "iota");
            Uri iotaUrl = new Uri(reposUri, "trunk/iota");
            Directory.CreateDirectory(tmp);
            File.WriteAllText(iota, "This is iota.\n");
            RunSvn("import", iota, iotaUrl.AbsoluteUri, "-m", "Import iota", "-q");

            File.AppendAllText(iota, "This is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\n");

            RunSvnMucc("put", iota, iotaUrl.AbsoluteUri, "-m", "Update iota");

            File.AppendAllText(iota, "This is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nFinal iota\n");

            RunSvnMucc(
                "put", iota, iotaUrl.AbsoluteUri,
                "propset", "prop", "val", iotaUrl.AbsoluteUri,
                "-m", "Update iota");


            RepositoryUrl = reposUri;
            RepositoryPath = dir;
        }
    }

    [TestMethod]
    public async Task ReadRev2()
    {
        string fsDir = Path.Combine(RepositoryPath, "db");

        var r2 = FileBucket.OpenRead(Path.Combine(fsDir, "revs/0/2"));

        using var rr = new SvnFsFsRevisionBucket(r2);

        await rr.ReadUntilEofAsync();
    }

    [TestMethod]
    public async Task ReadRev4()
    {
        string fsDir = Path.Combine(RepositoryPath, "db");

        var r3 = FileBucket.OpenRead(Path.Combine(fsDir, "revs/0/4"));

        using var rr = new SvnFsFsRevisionBucket(r3);

        await rr.ReadUntilEofAsync();
    }

    private string RunSvn(params string[] args)
    {
        return TestContext.RunApp("svn", args);
    }

    private string RunSvnAdmin(params string[] args)
    {
        return TestContext.RunApp("svnadmin", args);
    }

    private string RunSvnMucc(params string[] args)
    {
        return TestContext.RunApp("svnmucc", args);
    }
}
