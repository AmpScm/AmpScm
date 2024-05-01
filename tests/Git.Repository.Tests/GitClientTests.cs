using AmpScm.Git.Repository;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests;

[TestClass]
public class GitClientTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void HaveShellClient()
    {
        Assert.IsNotNull(GitConfiguration.GitProgramPath, "Have git executable");

        Assert.IsTrue(File.Exists(GitConfiguration.GitProgramPath));

        Assert.IsTrue(GitConfiguration.GitProgramVersion >= new Version(1, 0), "Have version of git >= 1.0");

        foreach (var v in GitConfiguration.GetGitConfigurationFilePaths(true))
        {
            TestContext.WriteLine(v);
        }
    }
}
