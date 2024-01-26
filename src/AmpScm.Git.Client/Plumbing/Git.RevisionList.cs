using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing;

public enum GitRevisionListOrder
{
    ReverseChronological,
    Date,
    AuthorDate,
    Topological
}

public class GitRevisionListArgs : GitPlumbingArgs
{
    public int? MaxCount { get; set; }

    public bool FirstParentOnly { get; set; }

    public List<string> Commits { get; set; } = new List<string>();

    public int? MaxParents { get; set; }
    public int? MinParents { get; set; }

    // Simplification

    public bool ShowPulls { get; set; }
    public bool FullHistory { get; set; }
    public bool Dense { get; set; }
    public bool Sparse { get; set; }
    public bool SimplifyMerges { get; set; }
    public bool AncestryPath { get; set; }


    // Ordering
    public GitRevisionListOrder Order {get;set;}
    public bool Reverse { get; set; }

    public override void Verify()
    {
        if (MaxCount < 0)
            throw new InvalidOperationException("MaxCount out of range");
    }
}

public partial class GitPlumbing
{
    [GitCommand("rev-list")]
    public static IAsyncEnumerable<GitId> RevisionList(this GitPlumbingClient c, GitRevisionListArgs options)
    {
        options.Verify();

        List<string> args = new List<string>();

        if (options.MaxCount != null)
            args.Add($"--max-count={options.MaxCount}");

        if (options.FirstParentOnly)
            args.Add("--first-parent");

        if (options.MaxParents != null)
            args.Add($"--max-parents={options.MaxParents.Value}");
        if (options.MinParents != null)
            args.Add($"--max-parents={options.MinParents.Value}");


        if (options.ShowPulls)
            args.Add("--show-pulls");
        if (options.FullHistory)
            args.Add("--full-history");
        if (options.Dense)
            args.Add("--dense");
        if (options.Sparse)
            args.Add("--sparse");
        if (options.SimplifyMerges)
            args.Add("--simplify-merges");
        if (options.AncestryPath)
            args.Add("--ancestry-path");
        if (options.Reverse)
            args.Add("--reverse");

        switch (options.Order)
        {
            case GitRevisionListOrder.ReverseChronological:
                break; // Default
            case GitRevisionListOrder.Date:
                args.Add("--date-order");
                break;
            case GitRevisionListOrder.AuthorDate:
                args.Add("--author-date-order");
                break;
            case GitRevisionListOrder.Topological:
                args.Add("--topo-order");
                break;
            default:
                throw new InvalidOperationException();
        }


        if (!options.Commits?.Any() ?? true)
        {
            args.Add("HEAD");
        }
        else
        {
            args.Add("--");
            args.AddRange(options.Commits!);
        }

        return c.Repository.WalkPlumbingCommand("rev-list", args).Select(x => GitId.TryParse(x, out var oid) ? oid : null!);
    }
}
