namespace AmpScm.Git.Client.Porcelain;

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


    internal static bool PerformReadOnlyCleanup { get; } = (AppDomain.CurrentDomain?.FriendlyName?.StartsWith("test", StringComparison.OrdinalIgnoreCase) ?? false);

    internal static void RemoveReadOnlyIfNecessary(string gitDirectory)
    {
        if (!PerformReadOnlyCleanup)
            return;

        string dir = Path.Combine(gitDirectory, "objects");

        if (Directory.Exists(dir))
            foreach (var v in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
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
