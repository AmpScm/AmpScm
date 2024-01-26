using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git.Repository;

// This file contains an internal API to allow exposing the GitRepository as a high level GitClient
// within the Amp.Git.Client package

[assembly: InternalsVisibleTo("AmpScm.Git.Client, PublicKey=00240000048000009400000006020000002400005253413100040000010001007599056e5c6e33684826385595eff712101ed7c16af9ed21f405e3c41625610af5ca4187716704279a030a47817165d5f0cf43ab67f00f3e6cf00abe69c8363d2486e5b0855c2b008086e931109da5dd158afd80b301c2cb02aee78ba8d6e718fc614e80614820c6d462c37108fdaf89c61479e5aa9eedb412816ce5949912b8")]

namespace AmpScm.Git
{
    public partial class GitRepository
    {
        internal GitRepository(GitRepositoryOpenArgs a)
            : this(GitRepositoryOpenArgs.NotNull(a).Path, a.RootType)
        {

        }

        internal static GitRepositoryOpenArgs InternalSetupArgs(string path)
        {
            return new GitRepositoryOpenArgs(path);
        }

        internal class GitRepositoryOpenArgs
        {
            public GitRepositoryOpenArgs(string path)
            {
                Path = path;
                RootType = GitRootType.None;
            }

            public string Path;

            public GitRootType RootType { get; }

            public static GitRepositoryOpenArgs NotNull(GitRepositoryOpenArgs a)
            {
                return a ?? throw new ArgumentNullException(nameof(a));
            }
        }

    }
}
