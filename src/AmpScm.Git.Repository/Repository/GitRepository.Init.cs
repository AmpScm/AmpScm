using System;
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
            File.WriteAllText(Path.Combine(gitDir, "HEAD"), $"ref: refs/heads/{branchName ?? GitRepositoryInitArgs.DefaultInitialBranchName}\n");

            const string ignoreCase = "\tignorecase = true\n";
            const string symLinks = "\tsymlinks = false\n";
            const string bareFalse = "\tbare = false\n";
            string configText = ""
                + "[core]\n"
                + "\trepositoryformatversion = 0\n"
                + "\tfilemode = false\n"
                + bareFalse
                + "\tlogallrefupdates = true\n"
                + symLinks
                + ignoreCase;

            if (init.Bare)
                configText = configText.Replace(bareFalse, bareFalse.Replace("false", "true", StringComparison.Ordinal), StringComparison.Ordinal);

            if (Environment.NewLine != "\r\n")
            {
                configText = configText.Replace(symLinks, "", StringComparison.Ordinal);
                configText = configText.Replace(ignoreCase, "", StringComparison.Ordinal);
            }

            File.WriteAllText(Path.Combine(gitDir, "config"), configText);

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

            return new GitRepository(path, init.Bare ? Repository.GitRootType.Bare : Repository.GitRootType.Normal);
        }

    }
}
