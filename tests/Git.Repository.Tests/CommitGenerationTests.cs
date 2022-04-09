using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace GitRepositoryTests
{
    [TestClass]
    public class CommitGenerationTests
    {
        [TestMethod]
        public void TestCommitChainValues()
        {
            for (int i = 0; i < 10; i++)
            {
                GitCommitGenerationValue cv = new GitCommitGenerationValue(i, DateTime.Now);

                Assert.AreEqual(i, cv.Generation);
                cv = GitCommitGenerationValue.FromValue(cv.Value);

                Assert.AreEqual(i, cv.Generation);
            }

            for (int i = 1970; i < 2100; i++)
            {
                GitCommitGenerationValue cv = new GitCommitGenerationValue(i, new DateTime(i, 2, 2, 0, 0, 0, DateTimeKind.Utc));

                Assert.AreEqual(i, cv.Generation);
                Assert.AreEqual(new DateTimeOffset(i, 2, 2, 0, 0, 0, TimeSpan.Zero), cv.CommitTime);

                // Values are together stored in an ulong, so recreate from there and test again
                cv = GitCommitGenerationValue.FromValue(cv.Value);

                Assert.AreEqual(i, cv.Generation);
                Assert.AreEqual(new DateTimeOffset(i, 2, 2, 0, 0, 0, TimeSpan.Zero), cv.CommitTime);
            }

        }
    }
}
