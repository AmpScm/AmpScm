using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitBucketTests
{
    [TestClass]
    public class GitStorageTests
    {
        public TestContext TestContext { get; set; } = default!;

        [TestMethod]
        public void CheckSignatureLines()
        {
            {
                GitSignatureRecord sig = new GitSignatureRecord { Name = "User", Email = "u@m", When = new DateTime(2002, 12, 5, 5, 6, 7, DateTimeKind.Utc) };

                Assert.AreEqual("User <u@m> 1039064767 +0000", sig.ToString());
                Assert.IsTrue(GitSignatureRecord.TryReadFromBucket(Encoding.UTF8.GetBytes(sig.ToString()), out var utcStartRb), "Can read record");
                Assert.AreEqual(sig, utcStartRb);
            }

            {
                GitSignatureRecord sig = new GitSignatureRecord { Name = "EU User", Email = "nl@eu", When = new DateTimeOffset(2003, 05, 27, 3, 3, 3, new TimeSpan(2, 0, 0)) };

                Assert.AreEqual("EU User <nl@eu> 1053997383 +0200", sig.ToString());
                Assert.IsTrue(GitSignatureRecord.TryReadFromBucket(Encoding.UTF8.GetBytes(sig.ToString()), out var cestStartRb), "Can read record");
                Assert.AreEqual(sig, cestStartRb);
            }


            {
                GitSignatureRecord sig = new GitSignatureRecord { Name = "India User ", Email = "india@in", When = new DateTimeOffset(2004, 04, 02, 3, 3, 3, new TimeSpan(5, 30, 0)) };

                Assert.AreEqual("India User  <india@in> 1080855183 +0530", sig.ToString());
                Assert.IsTrue(GitSignatureRecord.TryReadFromBucket(Encoding.UTF8.GetBytes(sig.ToString()), out var cestStartRb), "Can read record");
                Assert.AreEqual(sig, cestStartRb);
            }

            {
                GitSignatureRecord sig = new GitSignatureRecord { Name = "NegIndia User ", Email = "net-india@ni", When = new DateTimeOffset(2004, 04, 02, 3, 3, 3, -new TimeSpan(5, 30, 0)) };

                Assert.AreEqual("NegIndia User  <net-india@ni> 1080894783 -0530", sig.ToString());
                Assert.IsTrue(GitSignatureRecord.TryReadFromBucket(Encoding.UTF8.GetBytes(sig.ToString()), out var cestStartRb), "Can read record");
                Assert.AreEqual(sig, cestStartRb);
            }

            {
                GitSignatureRecord sig = new GitSignatureRecord { Name = " US User ", Email = " us@white space ", When = new DateTimeOffset(2003, 05, 27, 3, 3, 3, new TimeSpan(-6, 0, 0)) };

                Assert.AreEqual(" US User  < us@white space > 1054026183 -0600", sig.ToString());
                Assert.IsTrue(GitSignatureRecord.TryReadFromBucket(Encoding.UTF8.GetBytes(sig.ToString()), out var cestStartRb), "Can read record");
                Assert.AreEqual(sig, cestStartRb);
            }
        }
    }
}
