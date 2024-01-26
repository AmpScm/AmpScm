using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;

namespace AmpScm.Git.Objects;

internal interface IGitCommitGraphInfo
{
    IEnumerable<GitId> ParentIds { get; }
    GitCommitGenerationValue Value { get; }
}
