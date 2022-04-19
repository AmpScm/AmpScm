using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git
{
    public partial class GitRepository
    {
        public static GitRepository Init(string path)
            => Init(path, false);

        public static GitRepository Init(string path, bool isBare)
        {
            if (Directory.Exists(path) && (Directory.GetFiles(path).Any() || Directory.GetDirectories(path).Any()))
                throw new GitRepositoryException($"{path} already exists");

            // Quick and dirty setup minimal git repository
            string gitDir = path;
            if (!isBare)
            {
                gitDir = Path.Combine(path, ".git");
            }

            const string headBranchName = "master";

            Directory.CreateDirectory(Path.Combine(gitDir, "hooks"));
            Directory.CreateDirectory(Path.Combine(gitDir, "info"));
            Directory.CreateDirectory(Path.Combine(gitDir, "objects/info"));
            Directory.CreateDirectory(Path.Combine(gitDir, "objects/pack"));
            Directory.CreateDirectory(Path.Combine(gitDir, "refs/heads"));
            Directory.CreateDirectory(Path.Combine(gitDir, "refs/tags"));

            File.WriteAllText(Path.Combine(gitDir, "description"), "Unnamed repository; edit this file 'description' to name the repository." + Environment.NewLine);
            File.WriteAllText(Path.Combine(gitDir, "HEAD"), $"ref: refs/heads/{headBranchName}\n");

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

            if (isBare)
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

            if (!isBare)
                File.SetAttributes(gitDir, FileAttributes.Hidden | File.GetAttributes(gitDir));

            return new GitRepository(path, isBare ? Repository.GitRootType.Bare : Repository.GitRootType.Normal);
        }

    }
}
