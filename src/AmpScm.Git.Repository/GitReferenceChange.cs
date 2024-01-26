using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Git.Sets;

namespace AmpScm.Git;

[DebuggerDisplay("{TargetId.ToString(\"x12\"),nq} {Signature.When,nq} {Reason,nq}")]
public sealed class GitReferenceChange : IGitObject
{
    private object _signature;
    private GitRepository _repository;
    private GitObject? _targetObject;
    private GitObject? _originalObject;

    internal GitReferenceChange(GitRepository repository, GitReferenceLogRecord record)
    {
        _repository = repository;
        OriginalId = record.Original;
        TargetId = record.Target;
        _signature = record.Signature;
        Reason = record.Reason ?? "";
    }

    public GitId OriginalId { get; }
    public GitId TargetId { get; }
    public GitSignature Signature
        => (_signature as GitSignature) ?? (GitSignature)(_signature = new GitSignature((GitSignatureRecord)_signature));


    public GitObject? TargetObject => _targetObject ??= TargetId.IsZero ? null : _repository.Objects[TargetId];

    public GitObject? OriginalObject => _originalObject ??= OriginalId.IsZero ? null : _repository.Objects[OriginalId];

    public string Reason { get; }


    ValueTask IGitObject.ReadAsync()
    {
        GC.KeepAlive(TargetObject);
        GC.KeepAlive(OriginalObject);

        return default;
    }
}
