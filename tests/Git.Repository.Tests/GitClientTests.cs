using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git.Repository;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests
{
    [TestClass]
    public class GitClientTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void HaveShellClient()
        {
            Assert.IsNotNull(GitConfiguration.GitProgramPath);

            Assert.IsTrue(File.Exists(GitConfiguration.GitProgramPath));

            foreach(var v in GitConfiguration.GetGitConfigurationFilePaths(true))
            {
                TestContext.WriteLine(v);
            }
        }
    }
}
