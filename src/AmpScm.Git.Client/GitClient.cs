[assembly: CLSCompliant(true)]

namespace AmpScm.Git.Client;

public class GitClient : GitRepository
{
    public GitClient(string path)
        : base(InternalSetupArgs(path))
    {

    }
}
