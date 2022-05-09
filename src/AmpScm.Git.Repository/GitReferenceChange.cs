using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Git.Sets;

namespace AmpScm.Git
{
    [DebuggerDisplay("{TargetId.ToString(\"x12\"),nq} {Signature.When,nq} {Reason,nq}")]
    public sealed class GitReferenceChange : IGitObject
    {
        object _signature;
        internal GitReferenceChange(GitReferenceLogRecord record)
        {
            OriginalId = record.Original;
            TargetId = record.Target;
            _signature = record.Signature;
            Reason = record.Reason ?? "";
        }

        public GitId OriginalId { get; }
        public GitId TargetId { get; }
        public GitSignature Signature
            => (_signature as GitSignature) ?? (GitSignature)(_signature = new GitSignature((GitSignatureRecord)_signature));

        public string Reason { get; }


        ValueTask IGitObject.ReadAsync()
        {
            return default;
        }
    }
}
