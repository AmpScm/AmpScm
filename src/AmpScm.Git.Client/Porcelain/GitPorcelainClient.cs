using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Porcelain;

public class GitPorcelainClient
{
    public GitPorcelainClient(GitRepository repository)
    {
        Repository = repository;
    }

    internal GitRepository Repository { get; }

    internal ValueTask ThrowNotImplemented()
    {
        throw new NotImplementedException();
    }
}
