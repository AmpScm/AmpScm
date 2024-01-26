using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;
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

        var bb = await bucket.ReadExactlyAsync(8192);

        Assert.AreEqual(bb.Length, (int)l);

        bb = await innerBucket.ReadExactlyAsync(8192);

        Assert.AreEqual(4, bb.Length);
    }

}
