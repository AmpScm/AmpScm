using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AmpScm;
using AmpScm.Buckets;
using AmpScm.Git;
using AmpScm.Git.Client.Plumbing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests
{
    [TestClass]
    public class GitRepositoryTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void WalkCommitsEmpty()
        {
            using var repo = GitRepository.Init(TestContext.PerTestDirectory());

            Assert.IsNotNull(repo.FullPath);
            var items = repo.Commits.ToArray();

            Assert.IsNotNull(items.Length);

            var b = repo.Commits.Any();

            Assert.IsFalse(b);

            //repo.Commits.FirstOrDefault(x => x.Tree != null);
            //IAsyncIListProvider
            items = repo.Commits.Where(x => x != null).ToArray();

            Assert.IsNotNull(items.Length);

            Assert.AreEqual(false, repo.Configuration.GetBool("core", "bare") ?? false);

            Assert.IsNotNull(repo.Head);
            Assert.IsNull(repo.Head.Commit);
            Assert.IsNull(repo.Head.GitObject);
        }

        [TestMethod]
        public void TestBareOpen()
        {
            using var repo = GitRepository.Init(TestContext.PerTestDirectory(), true);
            Assert.IsTrue(repo.IsBare);

            var items = repo.Commits.ToArray();

            Assert.IsNotNull(items.Length);

            var b = repo.Commits.Any();

            Assert.IsFalse(b);

            //repo.Commits.FirstOrDefault(x => x.Tree != null);

            items = repo.Commits.Where(x => x != null).ToArray();

            Assert.IsNotNull(items.Length);

            Assert.AreEqual(true, repo.Configuration.GetBool("core", "bare") ?? false);

        }

        [TestMethod]
        public void PathNormalize()
        {
            var sd = Environment.SystemDirectory;

            if (string.IsNullOrEmpty(sd))
                return; // Not on Windows


            var normalized = GitTools.GetNormalizedFullPath(sd);

            Assert.AreEqual(normalized, GitTools.GetNormalizedFullPath(sd.ToUpperInvariant()));
            Assert.AreEqual(normalized, GitTools.GetNormalizedFullPath(sd.ToLowerInvariant()));
        }

        [TestMethod]
        public void OpenInner()
        {
            using var repoOuter = GitRepository.Init(TestContext.PerTestDirectory());
            using var repoInner = GitRepository.Init(Path.Combine(TestContext.PerTestDirectory(), "Inner"));

            string path = repoInner.FullPath;

            {
                using var repo2 = GitRepository.Open(path);
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }
            {
                using var repo2 = GitRepository.Open(Path.Combine(path, ".git"));
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }
            {
                using var repo2 = GitRepository.Open(Path.Combine(path, ".git"));
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }

            {
                using var repo2 = GitRepository.Open(Path.Combine(path, "a", "b", "c"));
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }

            Directory.Delete(Path.Combine(repoOuter.FullPath, ".git"), true);

            {
                using var repo2 = GitRepository.Open(path);
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }
            {
                using var repo2 = GitRepository.Open(Path.Combine(path, ".git"));
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }
            {
                using var repo2 = GitRepository.Open(Path.Combine(path, ".git"));
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }

            {
                using var repo2 = GitRepository.Open(Path.Combine(path, "a", "b", "c"));
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }
        }

        [TestMethod]
        public async Task WalkCommitsViaObjectRepository()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);

            //GC.KeepAlive(repo.Configuration.Identity);

            await foreach (var c in repo.ObjectRepository.GetAll<GitCommit>(new HashSet<GitId>()))
            {
                TestContext.WriteLine($"Commit {c.Id} - {GitTools.FirstLine(c.Message)}");
                if (c.Parent != null)
                    TestContext.WriteLine($" -parent {c.Parent?.Id} - {GitTools.FirstLine(c.Parent?.Message)}");
                TestContext.WriteLine($" -tree {c.Tree?.Id}");

                foreach (var v in c.Tree!)
                {
                    TestContext.WriteLine($"   - {v.Name}");
                }
            }
        }

        [TestMethod]
        public async Task WalkCommitsAsync()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);

            await foreach (var c in repo.Commits)
            {
                TestContext.WriteLine($"Commit {c.Id:x10} - {GitTools.FirstLine(c.Message)}");
                TestContext.WriteLine($"Author: {c.Author?.ToString() ?? "-"}");
                if (c.Parent != null)
                    TestContext.WriteLine($" -parent {c.Parent?.Id} - {GitTools.FirstLine(c.Parent?.Message)}");
                TestContext.WriteLine($" -tree {c.Tree.Id}");

                foreach (var v in c.Tree)
                {
                    TestContext.WriteLine($"   - {v.Name}");
                }
            }
        }

        [TestMethod]
        public void WalkCommits()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);

            foreach (var c in repo.Commits)
            {
                TestContext.WriteLine($"Commit {c.Id:x8} - {GitTools.FirstLine(c.Message)}");
                if (c.Parent != null)
                    TestContext.WriteLine($" -parent {c.Parent?.Id} - {GitTools.FirstLine(c.Parent?.Message)}");
                TestContext.WriteLine($" -tree {c.Tree?.Id}");

                foreach (var v in c.Tree!)
                {
                    TestContext.WriteLine($"   - {v.Name}");
                }
            }
        }

        [TestMethod]
        public async Task WalkRefLogHead()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);
            int n = 0;

            await foreach (var c in repo.Head.ReferenceChanges)
            {
                TestContext.WriteLine($"{c.OriginalId} {c.TargetId} {c.Signature}\t{c.Summary}");
                Assert.IsNotNull(c.OriginalId);
                Assert.IsNotNull(c.TargetId);
                Assert.IsNotNull(c.Signature);
                Assert.IsNotNull(c.Summary);
                n++;
            }

            Assert.IsTrue(repo.Head.ReferenceChanges.Last().Signature.When >= repo.Head.Commit!.Committer!.When);

            Assert.IsTrue(n > 0);
        }

        [TestMethod]
        public async Task WalkRefLogMaster()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);
            int n = 0;

            var rslv = repo.Head.Resolved;

            await foreach (var c in repo.Head.Resolved.ReferenceChanges)
            {
                TestContext.WriteLine($"{c.OriginalId} {c.TargetId} {c.Signature}\t{c.Summary}");
                Assert.IsNotNull(c.OriginalId);
                Assert.IsNotNull(c.TargetId);
                Assert.IsNotNull(c.Signature);
                Assert.IsNotNull(c.Summary);
                n++;
            }

            Assert.IsTrue(repo.Head.Resolved.ReferenceChanges.Last().Signature.When >= repo.Head.Commit!.Committer!.When);

            Assert.IsTrue(n > 0);
        }

        [TestMethod]
        public async Task WalkRefLogDetachedHead()
        {
            var dir = await TestContext.CreateCloneAsync();

            using var repo = GitRepository.Open(dir);
            await repo.GetPlumbing().RunRawCommand("checkout", new[] { "HEAD~1" });

            Assert.IsTrue(repo.IsHeadDetached, "Head detached");
            int n = 0;

            Assert.IsTrue(repo.Head.Resolved.ReferenceChanges.Last().Signature.When >= repo.Head.Commit!.Committer!.When);

            foreach(var t in repo.Tags)
            {
                TestContext.WriteLine($"tag: {t.Name}");
            }

            foreach (var b in repo.Branches)
            {
                TestContext.WriteLine($"branch: {b.Name}");
            }

            foreach (var r in repo.References)
            {
                TestContext.WriteLine($"references: {r.Name}");
            }

            Assert.IsTrue(repo.Tags.Count() > 0, "Has tags");
            Assert.IsTrue(repo.Tags.Count() < repo.References.Count(), "Has more references than tags");
            var f = repo.Tags.First();

            Assert.IsNotNull(repo.Head.GitObject, "Has GitObject");

            await foreach (var c in repo.Head.Resolved.ReferenceChanges)
            {
                TestContext.WriteLine($"{c.OriginalId} {c.TargetId} {c.Signature}\t{c.Summary}");
                Assert.IsNotNull(c.OriginalId);
                Assert.IsNotNull(c.TargetId);
                Assert.IsNotNull(c.Signature);
                Assert.IsNotNull(c.Summary);
                n++;
            }

            Assert.IsTrue(n > 0);
        }

        [TestMethod]
        public void WalkOne()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);

            var treeId = repo.Head.Commit!.Tree.Id.ToString();

            var tree = repo.Trees.First(x => x.Id.ToString() == treeId);

            foreach (var v in tree)
            {
                TestContext.WriteLine($"   - {v.Name} - {v.Id}");
            }

            foreach (var v in tree.AllItems)
            {
                TestContext.WriteLine($"# {v.Path}");
            }
        }

        public async ValueTask WalkSets_TestSet<TSet, TProp>(IQueryable<TProp> set, PropertyInfo pi, HashSet<Type> walked)
        {
            try
            {
                foreach (var v in set)
                {
                    break;
                }
            }
            catch (Exception e)
            {
                throw new AssertFailedException($"foreach on {pi.Name} works", e);
            }

            try
            {
                IEnumerable<TProp> set2 = set;

                GC.KeepAlive(set2.Any());
            }
            catch (Exception e)
            {
                throw new AssertFailedException($"enumerable Any on {typeof(TSet).Name}.{pi.Name} works", e);
            }

            try
            {
                set.Any();
            }
            catch (Exception e)
            {
                throw new AssertFailedException($"queryable Any on {typeof(TSet).Name}.{pi.Name} works", e);
            }

            if (set is IAsyncEnumerable<TProp> ae)
            {
                try
                {
                    ae.AnyAsync().AsTask().Wait();
                }
                catch (Exception e)
                {
                    throw new AssertFailedException($"async enumerable AnyAsync on {typeof(TSet).Name}.{pi.Name} works", e);
                }
            }

            if (set is IAsyncQueryable<TProp> aq)
            {
                try
                {
                    await aq.AnyAsync();
                }
                catch (Exception e)
                {
                    throw new AssertFailedException($"async queryable Any on {typeof(TSet).Name}.{pi.Name} works", e);
                }

                try
                {
                    aq.AnyAsync().AsTask().Wait();
                }
                catch (Exception e)
                {
                    throw new AssertFailedException($"async queryable AnyAsync on {typeof(TSet).Name}.{pi.Name} works", e);
                }
            }

            try
            {
                foreach (var v in set.Where(x => true).Take(2))
                { }
            }
            catch (Exception e)
            {
                throw new AssertFailedException($"queryable check on {typeof(TSet).Name}.{pi.Name} works", e);
            }


            WalkSets_TestType(set, walked);
        }

        public void WalkSets_TestType<T>(T instance, HashSet<Type> walked)
        {
            if (walked.Contains(typeof(T)))
                return;

            walked.Add(typeof(T));

            foreach (var prop in typeof(T).GetProperties().Where(prop => typeof(IQueryable).IsAssignableFrom(prop.PropertyType) && !prop.GetIndexParameters().Any()))
            {
                IQueryable v = (IQueryable)prop.GetValue(instance)!;
                Assert.IsNotNull(v, $"{typeof(T).Name}.{prop.Name} is not null");

                try
                {
                    typeof(GitRepositoryTests).GetMethod("WalkSets_TestSet")!.MakeGenericMethod(typeof(T), v.ElementType).Invoke(this, new object[] { v, prop, walked });
                }
                catch (Exception e)
                {
                    throw new TargetInvocationException($"When trying the {typeof(T).Name}.{prop.Name} property", e);
                }
            }

            foreach (var prop in typeof(T).GetProperties().Where(prop => !typeof(IQueryable).IsAssignableFrom(prop.PropertyType) && !prop.GetIndexParameters().Any()))
            {
                if (prop.PropertyType.IsGenericType && prop.PropertyType.IsValueType && prop.PropertyType.GetGenericTypeDefinition() == typeof(ReadOnlyMemory<>))
                    continue;

                object? ob;
                try
                {
                    ob = prop.GetValue(instance)!;
                }
                catch (Exception e)
                {
                    throw new AssertFailedException($"Fetching {typeof(T).Name}.{prop.Name} works", e);
                }

                try
                {
                    if (ob != null)
                    {
                        typeof(GitRepositoryTests).GetMethod(nameof(WalkSets_TestType))!.MakeGenericMethod(prop.PropertyType).Invoke(this, new object[] { ob, walked });
                    }
                }
                catch (Exception e)
                {
                    throw new TargetInvocationException($"When trying the {typeof(T).Name}.{prop.Name} property", e);
                }
            }
        }

        [TestMethod]
        public void WalkSetsEmptyRepository()
        {
            using var repo = GitRepository.Init(TestContext.PerTestDirectory());
            HashSet<Type> walked = new HashSet<Type>();

            WalkSets_TestType(repo, walked);
        }

        [TestMethod]
        public void WalkSetsDevRepository()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);
            HashSet<Type> walked = new HashSet<Type>();

            WalkSets_TestType(repo, walked);
        }
    }
}
