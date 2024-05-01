namespace AmpScm.Git.Client.Plumbing;

public class GitUpdateIndexArgs : GitPlumbingArgs
{
    public bool? SplitIndex { get; set; }
    public int? IndexVersion { get; set; }
    public bool Refresh { get; set; }
    public bool ReallyRefresh { get; set; }
    public bool Again { get; set; }

    public bool? UntrackedCache { get; set; }

    public override void Verify()
    {
        //throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("update-index")]
    public static async ValueTask UpdateIndex(this GitPlumbingClient c, GitUpdateIndexArgs? options = null)
    {
        options ??= new();
        options.Verify();

        List<string> args = new();

        if (options.SplitIndex == true)
            args.Add("--split-index");
        else if (options.SplitIndex == false)
            args.Add("--no-split-index");

        if (options.UntrackedCache == true)
            args.Add("--untracked-cache");
        else if (options.UntrackedCache == false)
            args.Add("--no-untracked-cache");

        if (options.Refresh)
            args.Add("--refresh");
        if (options.ReallyRefresh)
            args.Add("--really-refresh");
        if (options.Again)
            args.Add("--again");
        if (options.IndexVersion.HasValue)
            args.AddRange(new[] { "--index-version", options.IndexVersion.ToString()! });

        args.Add("--");


#if !NETFRAMEWORK
        if (OperatingSystem.IsWindows())
#else
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
#endif
        {
            string idx = Path.Combine(c.Repository.WorkTreeDirectory, "index");
            if (File.Exists(idx))
            {
                File.SetAttributes(idx, FileAttributes.Normal);
            }

            foreach (var v in Directory.EnumerateFiles(c.Repository.WorkTreeDirectory, "sharedindex.*"))
            {
                File.SetAttributes(v, FileAttributes.Normal);
            }
        }

        await c.Repository.RunGitCommandAsync("update-index", args);
    }
}
