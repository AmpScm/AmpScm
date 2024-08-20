using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitBucketTests;

[TestClass]
public class IndentTests
{
    [TestMethod]
    public async Task VerifyUnindentLength()
    {
        using var innerBucket = Bucket.Create.FromASCII(" i\n dent\n test\n more\nNext");

        using var bucket = new GitLineUnindentBucket(innerBucket.NoDispose());

        long l = (await bucket.ReadRemainingBytesAsync().ConfigureAwait(false)).Value;

        var bb = await bucket.ReadAtLeastAsync(8192, throwOnEndOfStream: false);

        Assert.AreEqual(bb.Length, (int)l);

        bb = await innerBucket.ReadAtLeastAsync(8192, throwOnEndOfStream: false);

        Assert.AreEqual(4, bb.Length);
    }

}
