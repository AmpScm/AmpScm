﻿namespace AmpScm.Git.Client.Plumbing;

public class GitIndexPackArgs : GitPlumbingArgs
{
    public bool? ReverseIndex { get; set; }
    public bool FixThin { get; set; }

    public override void Verify()
    {
    }
}

public partial class GitPlumbing
{
    [GitCommand("index-pack")]
    public static async ValueTask IndexPack(this GitPlumbingClient c, string path, GitIndexPackArgs? options = null)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        options ??= new GitIndexPackArgs();
        options.Verify();

        List<string> args = new();

        if (options.FixThin)
            args.Add("--fix-thin");

        if (options.ReverseIndex == true)
            args.Add("--rev-index");
        else if (options.ReverseIndex == false)
            args.Add("--no-rev-index");

        args.Add(path);

#if !NETFRAMEWORK
        if (OperatingSystem.IsWindows())
#else
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
#endif
        {
            string idx = Path.ChangeExtension(path, ".idx");
            if (File.Exists(idx))
            {
                File.SetAttributes(idx, FileAttributes.Normal);
                File.Delete(idx);
            }
        }

        await c.Repository.RunGitCommandAsync("index-pack", args).ConfigureAwait(false);
    }
}
