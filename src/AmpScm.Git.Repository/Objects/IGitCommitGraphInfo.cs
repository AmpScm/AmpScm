using AmpScm.Buckets.Git;

namespace AmpScm.Git.Objects;

internal interface IGitCommitGraphInfo
{
    IEnumerable<GitId> ParentIds { get; }
    GitCommitGenerationValue Value { get; }
}
