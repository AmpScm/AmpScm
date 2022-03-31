using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests
{

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
                    Console.WriteLine($"commit {r.Commit.Id}");
                    Console.WriteLine($"Author: {r.Commit.Author?.Name} <{r.Commit.Author?.Email}>");
                    Console.WriteLine($"Date:   {r.Commit.Author?.When}");
                    Console.WriteLine("");
                    Console.WriteLine(r.Commit.Message?.TrimEnd() + "\n");
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
                    Console.WriteLine($"commit {r.Commit.Id}");
                    Console.WriteLine($"Author: {r.Commit.Author?.Name} <{r.Commit.Author?.Email}>");
                    Console.WriteLine($"Date:   {r.Commit.Author?.When}");
                    Console.WriteLine("");
                    Console.WriteLine(r.Commit.Message?.TrimEnd() + "\n");
                }
            }
        }

    }
}
