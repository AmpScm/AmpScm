using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AmpScm;
using AmpScm.Git;
using AmpScm.Git.Client;
using AmpScm.Git.Client.Plumbing;
using AmpScm.Git.Client.Porcelain;
using AmpScm.Git.Repository;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests.Client
{
    [TestClass]
    public class GitPorcelainTests
    {
        public TestContext TestContext { get; set; } = null!;

        private static readonly string[] ignored = new[] {
            /* gui: */ "gitk", "citool", "instaweb", "gitweb", "bugreport", "gui",
            "annotate", /* folded in blame */
            "version", /* Not separate */
            "scalar", /* Not really a git command */
            /* experimental: */ "restore", "switch",
            "filter-branch"
        };

        [TestMethod]
        public async Task GitHelpToPorcelain()
        {
            using var repo = GitRepository.Init(TestContext.PerTestDirectory());

            var commandList = await repo.GetPlumbing().Help(new GitHelpArgs { Command = "-a" });

            var porcelainCommands = new HashSet<string>(
                typeof(GitPorcelain).GetMethods()
                    .Where(x => x.IsPublic && x.IsStatic)
                    .Select(x => x.GetCustomAttribute<GitCommandAttribute>()?.Name ?? "")
                    .Where(x => x.Length > 0)
                    .Distinct());

            var plumbingCommands = new HashSet<string>(
                typeof(GitPlumbing).GetMethods()
                    .Where(x => x.IsPublic && x.IsStatic)
                    .Select(x => x.GetCustomAttribute<GitCommandAttribute>()?.Name ?? "")
                    .Where(x => x.Length > 0)
                    .Distinct());

            StringBuilder sb = new StringBuilder();
            string? group = null;
            foreach (var command in commandList.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(command))
                    group = null;
                else if (char.IsLetterOrDigit(command, 1))
                    group = command;
                else if (group != null)
                {
                    var cmd = command.Trim().Split(' ')[0];
                    if (!group.StartsWith("Low-level") && !group.Contains("Internal") && !group.Contains("External commands") && !group.Contains("Interacting with")
                        && !group.Contains("file format") && !group.Contains("file interface")
                        && !plumbingCommands.Contains(cmd)
                        || porcelainCommands.Contains(cmd))
                    {
                        if (ignored.Contains(cmd) || cmd.EndsWith("tool"))
                            continue;

                        var parts = cmd.Split('-');

                        if (parts[0].StartsWith("mk"))
                            parts = new string[] { "make", parts[0].Substring(2) }.Concat(parts.Skip(1)).ToArray();

                        for (var i = 0; i < parts.Length; i++)
                        {
                            parts[i] = parts[i] switch
                            {
                                "ref" => "Reference",
                                "refs" => "References",
                                "reflog" => "ReferenceLog",
                                "ls" => "List",
                                "repo" => "Repository",
                                "rev" => "Revision",
                                "var" => "Variable",
                                "fsck" => "ConsistencyCheck",
                                "gc" => "GC",
                                "mv" => "Move",
                                "rm" => "Delete",
                                "am" => "ApplyMailbox", 
                                "whatchanged" => "WhatChanged",
                                "worktree" => "WorkTree",
                                "checkout" => "CheckOut",
                                "shortlog" => "ShortLog",
                                "submodule" => "SubModule",
                                _ => parts[i]
                            };
                        }

                        var name = string.Join("", parts.Select(x => x.Substring(0, 1).ToUpperInvariant() + x.Substring(1)));

                        if (!typeof(GitPorcelain).GetMethods().Any(x => x.Name == name))
                        {
                            sb.AppendLine($"Method {name} is missing on {nameof(GitPorcelain)}");
                            continue;
                        }
                        else if (typeof(GitPorcelain).Assembly.GetType(typeof(GitPorcelain).Namespace + $".Git{name}Args") == null)
                        {
                            sb.AppendLine($"Class Amp.Git.Client.Porcelain.Git{name}Args is missing");
                            continue;
                        }

                        var m = typeof(GitPorcelain).GetMethods().FirstOrDefault(x => x.Name == name && x.GetParameters().Length == 2);

                        if (m != null)
                        {
                            if (m.GetCustomAttributes<GitCommandAttribute>().FirstOrDefault() is GitCommandAttribute a)
                            {
                                Assert.AreEqual(cmd, a.Name, $"Gitcommand properly documented on {m.DeclaringType}.{m.Name}()");
                            }
                            else
                                Assert.Fail($"GitCommandAttribute not set on {m.DeclaringType}.{m.Name}()");

                            Assert.AreEqual($"Git{name}Args", m.GetParameters()[1].ParameterType.Name, "Parameter on {m.DeclaringType}.{m.Name}() as expected");
                        }
                    }
                }
            }
                if (sb.Length > 0)
                    Assert.Fail(sb.ToString());
        }


        public static IEnumerable<object[]> PorcelainCommandArgs => typeof(GitPorcelain).GetMethods().Where(x => x.GetCustomAttribute<GitCommandAttribute>() != null).Select(x => new[] { x });

        public static string PorcelainCommandName(MethodInfo mif, object[] args)
        {
            var mm = (MethodInfo)args[0];
            return mif.Name + "-" + mm?.DeclaringType?.Name + "." + mm?.Name;
        }

        private static readonly Regex reArgument = new Regex(@"--?[a-z0-9][a-z0-9-]*(\s*\<[^>]+\>)?(\s*[,|]\s*--?[a-z0-9][a-z0-9-]*\s*(\<[^>]+\>)?)*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        [TestMethod]
        [DynamicData(nameof(PorcelainCommandArgs), DynamicDataDisplayName = nameof(PorcelainCommandName))]
        public async Task VerifyUsage(MethodInfo m)
        {
            var gca = m.GetCustomAttribute<GitCommandAttribute>() ?? throw new AssertFailedException("Attribute not found");

            switch(m.Name)
            {
                case nameof(GitPorcelain.Diagnose) when GitConfiguration.GitProgramVersion < new Version(2,38):
                    Assert.Inconclusive($"git {m.Name} not supported by git {GitConfiguration.GitProgramVersion}");
                    return;
            }

            using (var repo = GitRepository.Open(Environment.CurrentDirectory))
            {
                var args = await repo.GetPlumbing().HelpUsage(gca.Name);
                var got = false;

                var argInfo = args.SkipWhile(x => !string.IsNullOrWhiteSpace(x)).Where(x => x.StartsWith("    -")).Select(x => x.TrimStart());

                foreach (var line in argInfo)
                {
                    var ma = reArgument.Match(line);

                    if (!ma.Success)
                        TestContext.WriteLine(line);
                    else
                        TestContext.WriteLine(ma.ToString());
                    got = true;
                }

                if (got)
                    return;

                if (args.Length > 0 && args[0].StartsWith("Usage:", StringComparison.OrdinalIgnoreCase))
                {
                    TestContext.WriteLine(args[0]);
                    return;
                }

                Assert.Inconclusive();
            }
        }
    }
}
