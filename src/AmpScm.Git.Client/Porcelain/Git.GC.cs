﻿namespace AmpScm.Git.Client.Porcelain;

public class GitGCArgs : GitPorcelainArgs
{
    public DateTime? PruneDate { get; set; }
    public bool Aggressive { get; set; }
    public bool Auto { get; set; }
    public bool Force { get; set; }
    public bool KeepLargestPack { get; set; }
    public override void Verify()
    {
        //throw new NotImplementedException();
    }
}

public partial class GitPorcelain
{
    [GitCommand("gc")]
    public static async ValueTask GC(this GitPorcelainClient c, GitGCArgs? options = null)
    {
        options ??= new();
        options.Verify();

        List<string> args = new List<string>();

        if (options.Aggressive)
            args.Add("--aggressive");
        if (options.Auto)
            args.Add("--auto");
        if (options.Force)
            args.Add("--force");
        if (options.KeepLargestPack)
            args.Add("--keep-largest-pack");
        if (options.PruneDate.HasValue)
            args.Add($"--prune={options.PruneDate.Value.Date.ToString("yyyy-MM-dd")}");
        await c.Repository.RunGitCommandAsync("gc", args).ConfigureAwait(false);

        RemoveReadOnlyIfNecessary(c.Repository.GitDirectory);
    }
}
