using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.References
{
    /// <summary>
    /// There are some custom backends used in some places to improve over the current packaged
    /// references. For now we fall back through to the shell handling as last resort to at least
    /// have all references, until we have implemented the new db format
    /// </summary>
    internal class GitShellReferenceRepository : GitPackedRefsReferenceRepository
    {
        public GitShellReferenceRepository(GitReferenceRepository repository, string gitDir, string workTreeDir)
            : base(repository, gitDir, workTreeDir)
        {
        }

        private protected override async ValueTask ReadRefs()
        {
            var (r, o) = await Repository.RunPlumbingCommandOut("show-ref", Array.Empty<string>(), expectedResults: new int[] { 0 /* ok */, 1 /* no references found */}).ConfigureAwait(false);

            if (r != 0)
                return;

            var idLength = GitId.HashLength(Repository.InternalConfig.IdType) * 2;

            GitRefPeel? last = null;
            foreach (string line in o.Split('\n'))
            {
                ParseLineToPeel(line.Trim(), ref last, idLength);
            }
        }
    }
}
