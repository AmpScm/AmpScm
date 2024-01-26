using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Sets;

public interface IGitObject
{
    ValueTask ReadAsync();
}

public interface IGitNamedObject : IGitObject
{
    public string Name { get; }
}
