using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing;

public class GitRepackArgs : GitPlumbingArgs
{
    public bool SinglePack { get; set; }
    public bool UnreachableAsLoose { get; set; }
    public bool RemoveUnused { get; set; }
    public bool Quiet { get; set; }
    public bool WriteBitmap { get; set; }
    public bool WriteMultiPack { get; set; }
    public int? GeometricFactor { get; set; }

    public int? Window { get; set; }
    public int? Depth { get; set; }
    public int? Threads { get; set; }

    public override void Verify()
    {
        //throw new NotImplementedException();
    }
}

public partial class GitPlumbing
{
    [GitCommand("repack")]
    public static async ValueTask Repack(this GitPlumbingClient c, GitRepackArgs? options = null)
    {
        var args = new List<string>();


        options?.Verify();
        options ??= new();

        if (options.SinglePack)
        {
            if (options.UnreachableAsLoose)
                args.Add("-A");
            else
                args.Add("-a");
        }
        if (options.RemoveUnused)
            args.Add("-d");
        if (options.Quiet)
            args.Add("-q");
        if (options.WriteBitmap)
            args.Add("--write-bitmap-index");
        if (options.WriteMultiPack)
            args.Add("--write-midx");
        if (options.GeometricFactor.HasValue)
            args.Add($"--geometric={options.GeometricFactor.Value}");

        if (options.Window.HasValue)
            args.Add($"--window={options.Window.Value}");
        if (options.Depth.HasValue)
            args.Add($"--depth={options.Depth.Value}");
        if (options.Threads.HasValue)
            args.Add($"--threads={options.Threads.Value}");

        await c.Repository.RunGitCommandAsync("repack", args);

        Porcelain.GitPorcelain.RemoveReadOnlyIfNecessary(c.Repository.GitDirectory);
    }
}
