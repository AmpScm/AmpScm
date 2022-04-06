
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AmpScm.Git;
using AmpScm.Git.Client.Plumbing;

namespace MyApp
{
    public class Program
    {
        public async static Task<int> Main(string[] args)
        {
            using var repo = GitRepository.Open(@"F:\git-testrepos\git-no-blob");

            if (repo.IsShallow)
                return -1;

            //var r = await repo.GetPlumbing().RevisionList(new GitRevisionListArgs { MaxCount = 32, FirstParentOnly = true }).ToListAsync();

            //Assert.AreEqual(32, r.Count);
            //Assert.AreEqual(32, r.Count(x => x != null));
            //Assert.AreEqual(32, r.Distinct().Count());

            var revs = repo.Head.Revisions.Take(32).Select(x => x.Commit.Id).ToList();

            //if (!r.SequenceEqual(revs))
            //{
            //    int? nDiff = null;
            //    for (int i = 0; i < Math.Min(revs.Count, r.Count); i++)
            //    {
            //        Console.WriteLine($"{i:00} {r[i]} - {revs[i]}");
            //
            //        if (!nDiff.HasValue && r[i] != revs[i])
            //            nDiff = i;
            //    }
            //    Debug.Fail($"Different list at {nDiff}");
            //}
            //
            //
            //if (repo.Commits[GitId.Parse("b71c6c3b64bc002731bc2d6c49080a4855d2c169")] is GitCommit manyParents)
            //{
            //    Console.WriteLine($"Found commit {manyParents}, so we triggered the many parent handling");
            //    manyParents.Revisions.Take(3).ToList();
            //
            //    Debug.Assert(manyParents.Parent!.ParentCount > 3);
            //}
            //
            //var id = repo.Head.Id!.ToString();
            //
            //for (int i = id.Length - 1; i > 7; i--)
            //{
            //    string searchVia = id.Substring(0, i);
            //    //Assert.IsNotNull(await repo.Commits.ResolveIdAsync(searchVia), $"Able to find via {searchVia}, len={i}");
            //}
            return 0;
        }
    }
}
