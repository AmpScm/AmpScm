﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git
{
    public sealed class GitRepositoryInitArgs
    {
        /// <summary>
        /// Currently "master", but will follow 'git'
        /// </summary>
        public static readonly string DefaultInitialBranchName = "master";
        public bool Bare { get; set; }
        public string? InitialBranchName { get; set; }

        public IEnumerable<(string, string)>? InitialConfiguration { get; set; }
        public GitIdType? IdType { get; set; }
    }

    public partial class GitRepository
    {
        public static GitRepository Init(string path)
            => Init(path, new GitRepositoryInitArgs());

        public static GitRepository Init(string path, bool isBare)
            => Init(path, new GitRepositoryInitArgs() { Bare = isBare });

        public static GitRepository Init(string path, GitRepositoryInitArgs? init)
        {
            if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
                throw new GitRepositoryException($"{path} already exists");
            if (init is null)
                init = new();

            // Quick and dirty setup minimal git repository
            string gitDir = path;
            if (!init.Bare)
            {
                gitDir = Path.Combine(path, ".git");
            }

            string? branchName = init.InitialBranchName;

            if (string.IsNullOrEmpty(branchName) || !branchName.Any(char.IsLetterOrDigit))
                branchName = GitRepositoryInitArgs.DefaultInitialBranchName;

            Directory.CreateDirectory(Path.Combine(gitDir, "hooks"));
            Directory.CreateDirectory(Path.Combine(gitDir, "info"));
            Directory.CreateDirectory(Path.Combine(gitDir, "objects/info"));
            Directory.CreateDirectory(Path.Combine(gitDir, "objects/pack"));
            Directory.CreateDirectory(Path.Combine(gitDir, "refs/heads"));
            Directory.CreateDirectory(Path.Combine(gitDir, "refs/tags"));

            File.WriteAllText(Path.Combine(gitDir, "description"), "Unnamed repository; edit this file 'description' to name the repository." + Environment.NewLine);

            bool sha256 = (init.IdType == GitIdType.Sha256);


            StringBuilder configText = new StringBuilder("[core]\n");

            if (!sha256)
                configText.Append("\trepositoryformatversion = 0\n");
            else
                configText.Append("\trepositoryformatversion = 1\n");

            configText.Append(""
                + "\tfilemode = false\n"
                + "\tlogallrefupdates = true\n");

            const string bareFalse = "\tbare = false\n";
            if (!init.Bare)
                configText.Append(bareFalse);
            else
                configText.Append(bareFalse.Replace("false", "true", StringComparison.Ordinal));

            if (File.Exists(Path.Combine(gitDir, "dEsCrIpTiOn")))
                configText.Append("\tignorecase = true\n");

            if (Environment.NewLine != "\r\n")
            {
                configText.Append("\tsymlinks = false\n");
            }

            if (sha256)
            {
                configText.Append("[extensions]\n\tobjectformat = sha256\n");
            }

            if (init.InitialConfiguration?.Any() ?? false)
            {
                string? lastGroup = null;

                foreach (var (k, v) in init.InitialConfiguration)
                {
                    var sp = k.Split(new[] { '.' }, 2);

                    if (sp.Length != 2)
                        continue;

                    if (!sp[0].Equals(lastGroup, StringComparison.OrdinalIgnoreCase))
                    {
                        lastGroup = sp[0];
                        configText.Append((string)$"\n[{lastGroup}]\n");
                    }
                    configText.Append((string)$"\t{sp[1]} = {v}\n");
                }
            }

            File.WriteAllText(Path.Combine(gitDir, "config"), configText.ToString());

            File.WriteAllText(Path.Combine(gitDir, "info/exclude"), ""
                + "# git ls-files --others --exclude-from=.git/info/exclude\n"
                + "# Lines that start with '#' are comments.\n"
                + "# For a project mostly in C, the following would be a good set of\n"
                + "# exclude patterns (uncomment them if you want to use them):\n"
                + "# *.[oa]\n"
                + "# *~\n"
            );

            if (!init.Bare)
                File.SetAttributes(gitDir, FileAttributes.Hidden | File.GetAttributes(gitDir));

            var r=  new GitRepository(path, init.Bare ? Repository.GitRootType.Bare : Repository.GitRootType.Normal);

            if (sha256)
                r.SetSHA256();

            branchName ??= r.Configuration.GetString("init", "defaultbranch") ?? GitRepositoryInitArgs.DefaultInitialBranchName;

            File.WriteAllText(Path.Combine(gitDir, "HEAD"), $"ref: refs/heads/{branchName}\n");

            return r;
        }

    }
}
