using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Objects;

public interface IGitLazy<out TGitObject>
    where TGitObject : GitObject
{
    GitId? Id { get; }

    GitObjectType Type { get; }

    ValueTask<GitId> WriteToAsync(GitRepository repository);
}
