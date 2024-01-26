using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests;


[TestClass]
public class GitHubSampleCode
{
    [TestMethod]
    public async Task WelcomeSample()
    {
        using (var repo = await GitRepository.OpenAsync(Environment.CurrentDirectory))
        {
            await foreach (var r in repo.Head.Revisions)
            {
                Trace.WriteLine($"commit {r.Commit.Id}");
                Trace.WriteLine($"Author: {r.Commit.Author?.Name} <{r.Commit.Author?.Email}>");
                Trace.WriteLine($"Date:   {r.Commit.Author?.When}");
                Trace.WriteLine("");
                Trace.WriteLine(r.Commit.Message?.TrimEnd() + "\n");
            }
        }
    }


    [TestMethod]
    public void WelcomeSampleNoAsync()
    {
        using (var repo = GitRepository.Open(Environment.CurrentDirectory))
        {
            foreach (var r in repo.Head.Revisions)
            {
                Trace.WriteLine($"commit {r.Commit.Id}");
                Trace.WriteLine($"Author: {r.Commit.Author?.Name} <{r.Commit.Author?.Email}>");
                Trace.WriteLine($"Date:   {r.Commit.Author?.When}");
                Trace.WriteLine("");
                Trace.WriteLine(r.Commit.Message?.TrimEnd() + "\n");
            }
        }
    }

}
