using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public class GitUpdateReferenceArgs : GitPlumbingArgs
    {
        public string? Message { get; set; }

        public bool CreateReferenceLog { get; set; } = true;

        public override void Verify()
        {
            //throw new NotImplementedException();
        }
    }

    public enum GitUpdateReferenceType
    {
        Update,
        Create,
        Delete,
        Verify,
        Option,
        Start,
        Prepare,
        Commit,
        Abort
    }

    public class GitUpdateReference
    {
        public GitUpdateReferenceType Type { get; set; } = GitUpdateReferenceType.Update;

        public string Name { get; set; } = default!;
        public GitId? Target { get; set; }
        public string? SymbolicTarget { get; set; }
        public GitId? OldTarget { get; set; }

        internal string ZeroString()
        {
            switch (Type)
            {
                case GitUpdateReferenceType.Update:
                    if (!GitReference.ValidName(Name, true))
                        throw new InvalidOperationException($"'{Name}' is not a valid reference name");
                    if (Target != null && OldTarget != null)
                        return $"update {Name}\0{Target}\0{OldTarget}\0";
                    else if (Target != null)
                        return $"update {Name}\0{Target}\0\0";
                    else if (SymbolicTarget != null)
                    {
                        if (!GitReference.ValidName(SymbolicTarget, true))
                            throw new InvalidOperationException($"'{SymbolicTarget}' is not a valid reference name");

                        return $"update {Name}\0{SymbolicTarget}\0";
                    }
                    else
                        throw new InvalidOperationException();
                default:
                    throw new NotImplementedException($"Update reference type {Type} not implemented yet");
            }
        }
    }

    partial class GitPlumbing
    {
        [GitCommand("update-ref")]
        public static async ValueTask UpdateReference(this GitPlumbingClient c, GitUpdateReference[] updates, GitUpdateReferenceArgs? options = null)
        {
            options ??= new GitUpdateReferenceArgs();
            options.Verify();

            List<string> args = new List<string>
            {
                "--stdin",
                "-z"
            };
            if (options.CreateReferenceLog)
                args.Add("--create-reflog");
            if (!string.IsNullOrWhiteSpace(options.Message))
            {
                args.Add("-m");
                args.Add(options.Message!);
            }

            await c.Repository.RunPlumbingCommand("update-ref", args.ToArray(),
                stdinText: string.Join("", updates.Select(x => x.ZeroString()).ToArray()));
        }

        [GitCommand("update-ref")]
        public static async ValueTask UpdateReference(this GitPlumbingClient c, GitUpdateReference update, GitUpdateReferenceArgs? a = null)
        {
            await UpdateReference(c, new[] { update }, a);
        }
    }
}
