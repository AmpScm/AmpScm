﻿using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace AmpScm.Git;

public enum GitReferenceFormatType
{
    None = 0,
    Files,
    ReferenceTable,
}

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

    public GitReferenceFormatType? ReferenceFormatType { get; set; }
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

        bool sha256 = (init.IdType == GitIdType.Sha256);
        bool refTable = (init.ReferenceFormatType == GitReferenceFormatType.ReferenceTable);

        Directory.CreateDirectory(Path.Combine(gitDir, "hooks"));
        Directory.CreateDirectory(Path.Combine(gitDir, "info"));
        Directory.CreateDirectory(Path.Combine(gitDir, "objects/info"));
        Directory.CreateDirectory(Path.Combine(gitDir, "objects/pack"));
        Directory.CreateDirectory(Path.Combine(gitDir, "refs/tags"));
        if (refTable)
        {
            Directory.CreateDirectory(Path.Combine(gitDir, "reftable"));
            File.WriteAllText(Path.Combine(gitDir, "refs/heads"), "this repository uses the reftable format\n");
        }
        else
        {
            Directory.CreateDirectory(Path.Combine(gitDir, "refs/heads"));
        }
        File.WriteAllText(Path.Combine(gitDir, "description"), "Unnamed repository; edit this file 'description' to name the repository." + Environment.NewLine);


        StringBuilder configText = new StringBuilder("[core]\n");
#pragma warning disable MA0011 // Use an overload of 'Append' that has a 'System.IFormatProvider' paramete

        if (!sha256 && !refTable)
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

        if (sha256 || refTable)
        {
            configText.Append("[extensions]\n");

            if (sha256)
                configText.Append("\tobjectformat = sha256\n");
            if (refTable)
                configText.Append("\trefstorage = reftable\n");
        }

        if (init.InitialConfiguration?.Any() ?? false)
        {
            string? lastGroup = null;

            foreach (var (k, v) in init.InitialConfiguration)
            {
                int n = k.LastIndexOf('.');

                if (n < 0)
                    continue;

                string group = k.Substring(0, n);
                string key = k.Substring(n + 1);

                if (!group.Equals(lastGroup, StringComparison.OrdinalIgnoreCase))
                {
                    lastGroup = group;

                    n = group.IndexOf('.', StringComparison.Ordinal);
                    if (n < 0)
                        configText.Append(CultureInfo.InvariantCulture, $"\n[{group}]\n");
                    else
                    {
                        var g = group.Substring(0, n);
                        var s = group.Substring(n + 1);
                        configText.Append(CultureInfo.InvariantCulture, $"\n[{g} \"{s}\"]\n");
                    }
                }
                configText.Append(CultureInfo.InvariantCulture, $"\t{key} = {v}\n");
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


        var r = new GitRepository(path, init.Bare ? Repository.GitRootType.Bare : Repository.GitRootType.Normal);

        if (sha256)
            r.SetSHA256();

        if (!init.Bare &&
            (r.Configuration.GetBool("core", "hidedotfiles") ?? string.Equals(r.Configuration.GetString("core", "hidedotfiles") ?? "dotgitonly", "dotgitonly", StringComparison.OrdinalIgnoreCase)))
        {
            File.SetAttributes(gitDir, FileAttributes.Hidden | File.GetAttributes(gitDir));
        }

        branchName ??= r.Configuration.GetString("init", "defaultbranch") ?? GitRepositoryInitArgs.DefaultInitialBranchName;

        if (!refTable)
        {
            File.WriteAllText(Path.Combine(gitDir, "HEAD"), $"ref: refs/heads/{branchName}\n");
        }
        else
        {
            File.WriteAllText(Path.Combine(gitDir, "HEAD"), $"ref: refs/heads/.invalid\n");
            var exitCode = r.RunGitCommandAsync("symbolic-ref", ["HEAD", $"refs/heads/{branchName}"]).AsTask().GetAwaiter().GetResult();

            Debug.Assert(exitCode == 0);
        }

        return r;
    }

}
