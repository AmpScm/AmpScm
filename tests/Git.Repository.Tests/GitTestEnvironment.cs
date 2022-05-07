using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Git;
using AmpScm.Git.Client.Plumbing;
using AmpScm.Git.Client.Porcelain;
using AmpScm.Git.Objects;
using AmpScm.Git.References;
using AmpScm.Git.Sets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests
{
    class RepoItem
    {
        public string Name { get; set; } = default!;
        public string? Content { get; set; }
    }

    public enum GitTestDir
    {
        Greek,
        Packed,
        MultiPack,
        MultiPackBitmap
    }


    [TestClass]
    public sealed class GitTestEnvironment
    {
        //public TestContext TestContext { get; set; } = default!;


        public static string TestRunReadOnlyDir { get; private set; } = default!;
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext ctx)
        {
            SetupRepositories(ctx.TestRunDirectory);
        }

        public static IEnumerable<GitTestDir> TestDirectories 
            => Enum.GetValues(typeof(GitTestDir)).Cast<GitTestDir>();

        private static void SetupRepositories(string testRunDirectory)
        {
            Task.Run(async () =>
            {
                var ro = Path.Combine(testRunDirectory, "base");
                while (Directory.Exists(ro))
                    ro += "_";

                Directory.CreateDirectory(ro);

                {
                    using var r = GitRepository.Init(Path.Combine(ro, "empty"));
                }
                {
                    using var r = GitRepository.Init(Path.Combine(ro, "bare"), true);
                }
            
                await CreateSvnTreeAsync(Path.Combine(ro, "greek-base"));

                {
                    using var p = GitRepository.Open(Path.Combine(ro, "greek-base"));

                    await p.GetPlumbing().RunRawCommand("clone", Path.Combine(ro, "greek-base"), Path.Combine(ro, "greek"));
                    await p.GetPlumbing().RunRawCommand("clone", Path.Combine(ro, "greek-base"), Path.Combine(ro, "greek-packed"));
                    await p.GetPlumbing().RunRawCommand("clone", Path.Combine(ro, "greek-base"), Path.Combine(ro, "greek-bmp"));
                    await p.GetPlumbing().RunRawCommand("clone", Path.Combine(ro, "greek-base"), Path.Combine(ro, "multipack"));
                    await p.GetPlumbing().RunRawCommand("clone", Path.Combine(ro, "greek-base"), Path.Combine(ro, "multipack-bmp"));
                }
                
                {
                    using var pp = GitRepository.Open(Path.Combine(ro, "greek-packed"));
                    await pp.GetPorcelain().GC(new GitGCArgs());
                }

                {
                    using var pp = GitRepository.Open(Path.Combine(ro, "greek-bmp"));
                    await pp.GetPorcelain().GC(new GitGCArgs());

                    await pp.GetPlumbing().Repack(new GitRepackArgs { WriteBitmap = true, SinglePack = true });
                }

                {
                    using var pp = GitRepository.Open(Path.Combine(ro, "multipack"));
                    await pp.GetPorcelain().GC(new GitGCArgs());
                    await pp.GetPlumbing().MultiPackIndex(new() { Command = GitMultiPackIndexCommand.Write });
                }

                {
                    using var pp = GitRepository.Open(Path.Combine(ro, "multipack-bmp"));
                    await pp.GetPorcelain().GC(new GitGCArgs());
                    await pp.GetPlumbing().MultiPackIndex(new() { Command = GitMultiPackIndexCommand.Write, Bitmap=true });
                }


                //{
                //    using var p = GitRepository.Open(Path.Combine(ro, "svn-base"));
                //
                //    await p.GetPlumbing().RunRawCommand("clone", Path.Combine(ro, "svn-base"), Path.Combine(ro, "svn-packed"));
                //
                //    using var pp = GitRepository.Open(Path.Combine(ro, "svn-packed"));
                //    await pp.GetPlumbing().GC(new GitGCArgs());
                //}

                TestRunReadOnlyDir = ro;
            }).Wait();
        }

        private static async Task CreateSvnTreeAsync(string v)
        {
            using var repo = GitRepository.Init(v);

            GitCommitWriter cw = GitCommitWriter.Create(new GitCommitWriter[0]);

            var items = new RepoItem[]
            {
                new RepoItem { Name = "iota", Content="This is dthe file 'iota'.\n" },
                new RepoItem { Name = "A" },
                new RepoItem { Name = "A/mu", Content="This is the file 'mu'.\n" },
                new RepoItem { Name = "A/B" },
                new RepoItem { Name = "A/B/lambda", Content="This is the file 'lambda'.\n"},
                new RepoItem { Name = "A/B/E", },
                new RepoItem { Name = "A/B/E/alpha", Content="This is the file 'alpha'.\n"},
                new RepoItem { Name = "A/B/E/beta", Content="This is the file 'beta'.\n" },
                new RepoItem { Name = "A/B/F" },
                new RepoItem { Name = "A/C" },
                new RepoItem { Name = "A/D" },
                new RepoItem { Name = "A/D/gamma", Content="This is the file 'gamma'.\n" },
                new RepoItem { Name = "A/D/G" },
                new RepoItem { Name = "A/D/G/pi", Content="This is the file 'pi'.\n" },
                new RepoItem { Name = "A/D/G/rho", Content="This is the file 'rho'.\n" },
                new RepoItem { Name = "A/D/G/tau", Content="This is the file 'tau'.\n" },
                new RepoItem { Name = "A/D/H" },
                new RepoItem { Name = "A/D/H/chi", Content = "This is the file 'chi'.\n" },
                new RepoItem { Name = "A/D/H/psi", Content = "This is the file 'psi'.\n" },
                new RepoItem { Name = "A/D/H/omega", Content = "This is the file 'omega'.\n" }
            };

            foreach (var item in items)
            {
                if (item.Content is not null)
                {
                    cw.Tree.Add(item.Name, GitBlobWriter.CreateFrom(System.Text.Encoding.UTF8.GetBytes(item.Content!).AsBucket()));
                }
                else
                {
                    cw.Tree.Add(item.Name, GitTreeWriter.CreateEmpty());
                }
            }

            cw.Author = cw.Committer = new GitSignature("BH", "bh@BH", new DateTimeOffset(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)));
            cw.CommitMessage = "Initial Commit";

            var cs = await cw.WriteAndFetchAsync(repo);

            cw = GitCommitWriter.Create(cs);

            cw.Author = cw.Committer = new GitSignature("BH", "bh@BH", new DateTimeOffset(new DateTime(2000, 1, 1, 1, 0, 0, DateTimeKind.Local)));
            cw.CommitMessage = "Second Commit";

            cs = await cw.WriteAndFetchAsync(repo);


            var ct = GitTagObjectWriter.Create(cs, "v0.1");
            ct.TagMessage = "Tag second Commit";
            ct.Tagger = new GitSignature("BH", "bh@BH", new DateTimeOffset(new DateTime(2000, 1, 2, 0, 0, 0, DateTimeKind.Local)));

            var tag = await ct.WriteAndFetchAsync(repo);

            await repo.GetPlumbing().UpdateReference(
                new GitUpdateReference { Name = $"refs/tags/{tag.Name}", Target = ct.Id },
                new GitUpdateReferenceArgs { Message = "Apply tag" });

            var baseId = cs.Id;


            cw = GitCommitWriter.Create(cs);
            cw.Author = cw.Committer = new GitSignature("BH", "bh@BH", new DateTimeOffset(new DateTime(2000, 1, 3, 0, 0, 0, DateTimeKind.Local)));
            cw.Tree.Add("A/C/delta", GitBlobWriter.CreateFrom(Encoding.UTF8.GetBytes("This is the file 'delta'.\n").AsBucket()));
            cw.CommitMessage = "Committed on branch";

            var alt = await cw.WriteAndFetchAsync(repo);

            cw = GitCommitWriter.Create(cs, alt);
            cw.Author = cw.Committer = new GitSignature("BH", "bh@BH", new DateTimeOffset(new DateTime(2000, 1, 4, 0, 0, 0, DateTimeKind.Local)));
            cw.CommitMessage = "Merged";

            cs = await cw.WriteAndFetchAsync(repo);

            string? refName = ((GitSymbolicReference)repo.Head).ReferenceName;
            Assert.AreEqual("refs/heads/master", refName);
            await repo.GetPlumbing().UpdateReference(
                new GitUpdateReference { Name = refName!, Target = cs.Id },
                new GitUpdateReferenceArgs { Message = "Testing" });

            await repo.GetPlumbing().RunRawCommand("checkout", "HEAD", ".");
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            if (!string.IsNullOrEmpty(TestRunReadOnlyDir))
            {
                try
                {
                    foreach(var f in Directory.GetFiles(TestRunReadOnlyDir, "*", SearchOption.AllDirectories))
                    {
                        var a = File.GetAttributes(f);
                        if ((a & (FileAttributes.ReadOnly | FileAttributes.System)) != 0)
                            File.SetAttributes(f, FileAttributes.Normal);
                    }
                    Directory.Delete(TestRunReadOnlyDir, true);
                }
                catch { }
            }
        }    
        
        public static string GetRepository(GitTestDir dir)
        {
            return Path.Combine(TestRunReadOnlyDir,
                dir switch
                {
                    GitTestDir.Greek => "greek",
                    GitTestDir.Packed => "greek-packed",
                    GitTestDir.MultiPack => "multipack",
                    GitTestDir.MultiPackBitmap => "multipack-bmp",
                    _ => throw new ArgumentOutOfRangeException(nameof(dir))
                });
        }
    }
}
