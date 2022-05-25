using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Porcelain
{
    public abstract class GitPorcelainArgs
    {
        public abstract void Verify();
    }

    public static partial class GitPorcelain
    {
        public static GitPorcelainClient GetPorcelain(this GitRepository repository)
        {
            return new GitPorcelainClient(repository);
        }



        internal static void RemoveReadOnlyIfNecessary(GitRepository repository)
        {
            string dir = Path.Combine(repository.GitDirectory, "objects");

            if (Directory.Exists(dir))
                foreach (var v in Directory.GetFiles(dir, "????*"))
                {
                    switch (Path.GetExtension(v))
                    {
                        case ".pack":
                        case ".idx":
                        case ".bitmap":
                        case ".graph":
                        case "":
                            File.SetAttributes(v, FileAttributes.Normal);
                            break;
                    }
                }
        }
    }
}
