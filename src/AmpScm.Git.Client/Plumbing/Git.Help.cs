﻿using System.Reflection;
using AmpScm.Git.Client.Porcelain;

namespace AmpScm.Git.Client.Plumbing;

public class GitHelpArgs : GitPlumbingArgs
{
    public string? Command { get; set; }
    public string? Guide { get; set; }

    public override void Verify()
    {
        if (!(string.IsNullOrEmpty(Command) ^ string.IsNullOrEmpty(Guide)))
            throw new ArgumentOutOfRangeException($"{nameof(Command)} or {nameof(Guide)} should be set");
    }
}

public partial class GitPlumbing
{
    [GitCommand("help")]
    public static async ValueTask<string> Help(this GitPlumbingClient c, GitHelpArgs options)
    {
        options.Verify();
        string[] args;

        if (options.Command == "-a")
            args = new string[] { "-a" };
        else
            args = new string[] { "-i", options.Command! ?? options.Guide! };

        var (_, txt) = await c.Repository.RunGitCommandOutAsync("help", args).ConfigureAwait(false);

        return txt ?? "";
    }

    public static async ValueTask<string[]> HelpUsage(this GitPlumbingClient c, string name)
    {
        if (!typeof(GitPlumbing).GetMethods().Any(x => x.GetCustomAttribute<GitCommandAttribute>()?.Name == name)
            && !typeof(GitPorcelain).GetMethods().Any(x => x.GetCustomAttribute<GitCommandAttribute>()?.Name == name))
            throw new ArgumentOutOfRangeException(nameof(name));

        List<string> results = new List<string>();
        bool gotOne = false;
        await foreach (var line in c.Repository.WalkPlumbingCommand(name, new[] { "-h" }, expectedResults: new[] { 129, 0 }).ConfigureAwait(false))
        {
            results.Add(line);
            if (!gotOne && !string.IsNullOrWhiteSpace(line))
                gotOne = true;
        }

        if (gotOne)
            return results.ToArray();

        var (_, _, stderr) = await c.Repository.RunGitCommandErrAsync(name, new[] { "-h" }, expectedResults: new[] { 129 }).ConfigureAwait(false);

        return stderr.Split('\n').Select(x=>x.TrimEnd()).ToArray();
    }
}
